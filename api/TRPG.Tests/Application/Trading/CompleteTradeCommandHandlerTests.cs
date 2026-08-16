using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Trading;
using TRPG.Application.Trading.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Trading;

[Collection("Database")]
public sealed class CompleteTradeCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CompleteTradeCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Workstation _workstation = Builders.MakeWorkstation(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CompleteTradeCommandHandler>();

        _context.Creatures.Add(_player);
        _context.Props.Add(_workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItem(Item item, Guid ownerId, OwnerType ownerType)
    {
        item.Ownership.OwnerId = ownerId;
        item.Ownership.OwnerType = ownerType;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Handle_SwapsOfferedItems_WhenOfferIsAccepted()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeaponItem(WorldId, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmorItem(WorldId, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );
        var command = new CompleteTradeCommand
        {
            PlayerId = _player.Id,
            WorldId = WorldId,
            WorkstationId = _workstation.Id,
            PlayerOffer = [new ItemSelection(playerItem.Id, 1)],
            ShopOffer = [new ItemSelection(shopItem.Id, 1)],
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var transferredPlayerItem = await verifyContext.Items.SingleAsync(
            i => i.Id == playerItem.Id,
            TestContext.Current.CancellationToken
        );
        var transferredShopItem = await verifyContext.Items.SingleAsync(
            i => i.Id == shopItem.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_workstation.Id, transferredPlayerItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Workstation, transferredPlayerItem.Ownership.OwnerType);
        Assert.Equal(_player.Id, transferredShopItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, transferredShopItem.Ownership.OwnerType);
    }

    [Fact]
    public async Task Handle_ThrowsWithoutTransferringItems_WhenOfferIsRejected()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeArmorItem(WorldId, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeWeaponItem(WorldId, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );
        var command = new CompleteTradeCommand
        {
            PlayerId = _player.Id,
            WorldId = WorldId,
            WorkstationId = _workstation.Id,
            PlayerOffer = [new ItemSelection(playerItem.Id, 1)],
            ShopOffer = [new ItemSelection(shopItem.Id, 1)],
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        await using var verifyContext = db.CreateContext();
        var unchangedPlayerItem = await verifyContext.Items.SingleAsync(
            i => i.Id == playerItem.Id,
            TestContext.Current.CancellationToken
        );
        var unchangedShopItem = await verifyContext.Items.SingleAsync(
            i => i.Id == shopItem.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.Id, unchangedPlayerItem.Ownership.OwnerId);
        Assert.Equal(_workstation.Id, unchangedShopItem.Ownership.OwnerId);
    }
}
