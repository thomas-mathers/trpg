using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetInventorySummaryByOwnerQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetInventorySummaryByOwnerQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetInventorySummaryByOwnerQueryHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItem(Item item, int quantity)
    {
        item.Quantity = quantity;
        item.Ownership.OwnerId = _creature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Handle_ReturnsOwnersGold()
    {
        // Arrange
        await SeedItem(Builders.MakeGold(_creature.WorldId), 100);

        // Act
        var result = await _handler.Handle(
            new GetInventorySummaryByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(100, result.Gold);
    }

    [Fact]
    public async Task Handle_ReturnsZeroGold_WhenOwnerDoesNotExist()
    {
        // Act — no existence check by design; an unknown creature id just has no gold or items
        var result = await _handler.Handle(
            new GetInventorySummaryByOwnerQuery
            {
                Owner = new ItemOwnerReference(Guid.NewGuid(), OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result.Gold);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_ReturnsEveryItem()
    {
        // Arrange
        var weapon = await SeedItem(Builders.MakeWeaponItem(_creature.WorldId), 1);
        var potion = await SeedItem(Builders.MakeConsumableItem(_creature.WorldId), 3);

        // Act
        var result = await _handler.Handle(
            new GetInventorySummaryByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.Id == weapon.Id);
        Assert.Contains(result.Items, i => i.Id == potion.Id);
    }
}
