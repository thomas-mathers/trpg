using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class AddGoldCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddGoldCommandHandler _handler = null!;
    private readonly Creature _owner = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddGoldCommandHandler>();

        _context.Creatures.Add(_owner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesGold_WhenOwnerHasNone()
    {
        // Arrange
        var command = new AddGoldCommand
        {
            Owner = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
            WorldId = WorldId,
            Amount = 10,
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item =>
                    item.Ownership.OwnerId == _owner.Id
                    && item.Ownership.OwnerType == OwnerType.Creature,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(10, gold.Quantity);
        Assert.Equal(_owner.Id, gold.Ownership.OwnerId);
    }

    [Fact]
    public async Task Handle_IncreasesGold_WhenOwnerAlreadyHasGold()
    {
        // Arrange
        var gold = Builders.MakeGold(WorldId, quantity: 5);
        gold.Ownership.OwnerId = _owner.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new AddGoldCommand
        {
            Owner = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
            WorldId = WorldId,
            Amount = 10,
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item =>
                    item.Ownership.OwnerId == _owner.Id
                    && item.Ownership.OwnerType == OwnerType.Creature,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(15, updatedGold.Quantity);
    }
}
