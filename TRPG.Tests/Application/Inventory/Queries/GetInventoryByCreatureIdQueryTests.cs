using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetInventoryByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetInventoryByCreatureIdQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Item _item = Builders.MakeItem();
    private readonly Item _otherItem = Builders.MakeItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetInventoryByCreatureIdQueryHandler(_context);

        _context.Creatures.Add(_creature);
        _context.Items.AddRange(_item, _otherItem);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCollection_WhenCreatureHasNoItems()
    {
        // Act
        var items = await _handler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task Handle_ReturnsItemsOrderedByAcquiredAt()
    {
        // Arrange
        _item.Quantity = 1;
        _item.Ownership.OwnerId = _creature.Id;
        _item.Ownership.OwnerType = OwnerType.Creature;
        _item.Ownership.AcquiredAt = DateTime.UtcNow.AddMinutes(-1);
        _otherItem.Quantity = 1;
        _otherItem.Ownership.OwnerId = _creature.Id;
        _otherItem.Ownership.OwnerType = OwnerType.Creature;
        _otherItem.Ownership.AcquiredAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var items = await _handler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([_item.Id, _otherItem.Id], items.Select(i => i.Id));
    }
}
