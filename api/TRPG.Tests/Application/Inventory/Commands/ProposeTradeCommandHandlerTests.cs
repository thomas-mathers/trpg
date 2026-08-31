using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class ProposeTradeCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ProposeTradeCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _shopkeeper = Builders.MakeCreature(WorldId);
    private Workstation _workstation = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ProposeTradeCommandHandler>();

        _workstation = Builders.MakeWorkstation(WorldId, assignedCreatureId: _shopkeeper.Id);
        _context.Creatures.AddRange(_player, _shopkeeper);
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
    public async Task Handle_ReturnsRefused_WhenTheAssignedCreatureIsHostileTowardThePlayer()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmor(WorldId, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _shopkeeper.Id,
                TargetType = ReputationTargetType.Creature,
                Score = -75,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var command = new ProposeTradeCommand
        {
            PlayerId = _player.Id,
            WorkstationId = _workstation.Id,
            PlayerOffer = [new ItemSelection(playerItem.Id, 1)],
            ShopOffer = [new ItemSelection(shopItem.Id, 1)],
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TradeOutcome.Refused, result);
    }

    [Fact]
    public async Task Handle_EvaluatesTheOffer_WhenReputationIsNotHostile()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmor(WorldId, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );
        var command = new ProposeTradeCommand
        {
            PlayerId = _player.Id,
            WorkstationId = _workstation.Id,
            PlayerOffer = [new ItemSelection(playerItem.Id, 1)],
            ShopOffer = [new ItemSelection(shopItem.Id, 1)],
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(TradeOutcome.Refused, result);
    }
}
