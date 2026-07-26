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
    private readonly Creature _corpse = Builders.MakeCreature(
        WorldId,
        roomId: RoomId,
        state: CreatureState.Dead,
        gold: 100
    );
    private readonly Creature _player = Builders.MakeCreature(WorldId, roomId: RoomId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<InventoryTransferCommandHandler>();

        _context.Creatures.AddRange(_corpse, _player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItemOnCorpse(int quantity)
    {
        var item = Builders.MakeWeaponItem(WorldId);
        _context.Items.Add(item);
        _context.InventoryItems.Add(
            new InventoryItem
            {
                WorldId = WorldId,
                CreatureId = _corpse.Id,
                ItemId = item.Id,
                Quantity = quantity,
                Index = 0,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Handle_MovesGold_WhenTakeGoldIsTrue()
    {
        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _corpse.Id,
                ToCreatureId = _player.Id,
                TakeGold = true,
                Items = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpse = await verifyContext.Creatures.FindAsync(
            [_corpse.Id],
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
    public async Task Handle_LeavesGoldUntouched_WhenTakeGoldIsFalse()
    {
        // Arrange
        var item = await SeedItemOnCorpse(quantity: 1);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _corpse.Id,
                ToCreatureId = _player.Id,
                TakeGold = false,
                Items = [new LootItemSelection(item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpse = await verifyContext.Creatures.FindAsync(
            [_corpse.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, corpse!.Gold);
    }

    [Fact]
    public async Task Handle_MovesSelectedItems_FromCorpseToPlayer()
    {
        // Arrange
        var item = await SeedItemOnCorpse(quantity: 3);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _corpse.Id,
                ToCreatureId = _player.Id,
                TakeGold = false,
                Items = [new LootItemSelection(item.Id, 3)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var corpseHasItem = await verifyContext.InventoryItems.AnyAsync(
            i => i.CreatureId == _corpse.Id && i.ItemId == item.Id,
            TestContext.Current.CancellationToken
        );
        var playerItem = await verifyContext.InventoryItems.SingleAsync(
            i => i.CreatureId == _player.Id && i.ItemId == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(corpseHasItem);
        Assert.Equal(3, playerItem.Quantity);
    }

    [Fact]
    public async Task Handle_MovesBothGoldAndItems_WhenBothAreRequested()
    {
        // Arrange
        var item = await SeedItemOnCorpse(quantity: 1);

        // Act
        await _handler.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = _corpse.Id,
                ToCreatureId = _player.Id,
                TakeGold = true,
                Items = [new LootItemSelection(item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        var playerHasItem = await verifyContext.InventoryItems.AnyAsync(
            i => i.CreatureId == _player.Id && i.ItemId == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, player!.Gold);
        Assert.True(playerHasItem);
    }

    [Fact]
    public async Task Handle_Throws_WhenFromCreatureIsNotACorpse()
    {
        // Arrange
        var livingCreature = Builders.MakeCreature(WorldId, roomId: RoomId);
        _context.Creatures.Add(livingCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new InventoryTransferCommand
                {
                    FromCreatureId = livingCreature.Id,
                    ToCreatureId = _player.Id,
                    TakeGold = true,
                    Items = [],
                },
                TestContext.Current.CancellationToken
            )
        );
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
                    FromCreatureId = _corpse.Id,
                    ToCreatureId = farPlayer.Id,
                    TakeGold = true,
                    Items = [],
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
