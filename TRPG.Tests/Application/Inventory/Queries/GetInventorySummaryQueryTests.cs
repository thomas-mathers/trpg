using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetInventorySummaryQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddInventoryItemCommandHandler _addInventoryItem = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetInventorySummaryQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(gold: 100);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _addInventoryItem = _serviceProvider.GetRequiredService<AddInventoryItemCommandHandler>();
        _handler = _serviceProvider.GetRequiredService<GetInventorySummaryQueryHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItem(Item item)
    {
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Handle_ReturnsTheCreaturesGold()
    {
        // Act
        var result = await _handler.Handle(
            new GetInventorySummaryQuery { CreatureId = _creature.Id, ConsumableOnly = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(100, result.Gold);
    }

    [Fact]
    public async Task Handle_ReturnsZeroGold_ForUnknownCreatureId()
    {
        // Act — no existence check by design; an unknown creature id just has no gold or items
        var result = await _handler.Handle(
            new GetInventorySummaryQuery { CreatureId = Guid.NewGuid(), ConsumableOnly = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result.Gold);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_ReturnsEveryItem_WhenConsumableOnlyIsFalse()
    {
        // Arrange
        var weapon = await SeedItem(Builders.MakeWeaponItem(_creature.WorldId));
        var potion = await SeedItem(Builders.MakeConsumableItem(_creature.WorldId));
        await _addInventoryItem.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = weapon.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );
        await _addInventoryItem.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = potion.Id,
                Quantity = 3,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetInventorySummaryQuery { CreatureId = _creature.Id, ConsumableOnly = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.ItemId == weapon.Id);
        Assert.Contains(result.Items, i => i.ItemId == potion.Id);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyConsumables_WhenConsumableOnlyIsTrue()
    {
        // Arrange
        var weapon = await SeedItem(Builders.MakeWeaponItem(_creature.WorldId));
        var potion = await SeedItem(Builders.MakeConsumableItem(_creature.WorldId));
        await _addInventoryItem.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = weapon.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );
        await _addInventoryItem.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = potion.Id,
                Quantity = 3,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var result = await _handler.Handle(
            new GetInventorySummaryQuery { CreatureId = _creature.Id, ConsumableOnly = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(potion.Id, item.ItemId);
    }
}
