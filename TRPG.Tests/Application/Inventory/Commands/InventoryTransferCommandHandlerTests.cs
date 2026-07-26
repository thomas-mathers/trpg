using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Commands;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class InventoryTransferCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private InventoryTransferCommandHandler _handler = null!;
    private readonly Creature _fromCreature = Builders.MakeCreature(WorldId, roomId: RoomId);
    private readonly Creature _player = Builders.MakeCreature(WorldId, roomId: RoomId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<InventoryTransferCommandHandler>();

        _context.Creatures.AddRange(_fromCreature, _player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItemOnFromCreature(int quantity)
    {
        var item = Builders.MakeWeaponItem(WorldId);
        item.Quantity = quantity;
        item.Ownership.OwnerId = _fromCreature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private async Task<Gold> SeedGoldOnFromCreature(int quantity)
    {
        var gold = Builders.MakeGold(WorldId, quantity);
        gold.Ownership.OwnerId = _fromCreature.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        _fromCreature.Gold = quantity;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gold;
    }

    [Fact]
    public async Task Handle_MovesGold_WhenGoldItemIsSelected()
    {
        // Arrange
        var gold = await SeedGoldOnFromCreature(100);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _fromCreature.Id,
                ToCreatureId = _player.Id,
                Items = [new LootItemSelection(gold.Id, 100)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpse = await verifyContext.Creatures.FindAsync(
            [_fromCreature.Id],
            TestContext.Current.CancellationToken
        );
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, corpse!.Gold);
        Assert.Equal(100, player!.Gold);
    }

    [Fact]
    public async Task Handle_LeavesGoldUntouched_WhenGoldIsNotSelected()
    {
        // Arrange
        await SeedGoldOnFromCreature(100);
        var item = await SeedItemOnFromCreature(quantity: 1);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _fromCreature.Id,
                ToCreatureId = _player.Id,
                Items = [new LootItemSelection(item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpse = await verifyContext.Creatures.FindAsync(
            [_fromCreature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, corpse!.Gold);
    }

    [Fact]
    public async Task Handle_MovesSelectedItems_FromCorpseToPlayer()
    {
        // Arrange
        var item = await SeedItemOnFromCreature(quantity: 3);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _fromCreature.Id,
                ToCreatureId = _player.Id,
                Items = [new LootItemSelection(item.Id, 3)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(3, movedItem.Quantity);
    }

    [Fact]
    public async Task Handle_MovesBothGoldAndItems_WhenBothAreSelected()
    {
        // Arrange
        var gold = await SeedGoldOnFromCreature(100);
        var item = await SeedItemOnFromCreature(quantity: 1);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _fromCreature.Id,
                ToCreatureId = _player.Id,
                Items = [new LootItemSelection(gold.Id, 100), new LootItemSelection(item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, player!.Gold);
        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
    }

    [Fact]
    public async Task Handle_Throws_WhenPlayerIsNotInTheSameRoom()
    {
        // Arrange
        var farPlayer = Builders.MakeCreature(WorldId, roomId: Guid.NewGuid());
        _context.Creatures.Add(farPlayer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new InventoryTransferCommand
                {
                    FromCreatureId = _fromCreature.Id,
                    ToCreatureId = farPlayer.Id,
                    Items = [],
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
