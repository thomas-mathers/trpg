using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetInventoryByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddInventoryItemCommandHandler _addHandler = null!;
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private GetInventoryByCreatureIdQueryHandler _handler = null!;
    private Item _item = null!;
    private Item _otherItem = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addHandler = new AddInventoryItemCommandHandler(_context);
        _handler = new GetInventoryByCreatureIdQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _item = Builders.MakeItem();
        _otherItem = Builders.MakeItem();

        _context.Creatures.Add(_creature);
        _context.Items.Add(_item);
        _context.Items.Add(_otherItem);

        await _context.SaveChangesAsync();
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
    public async Task Handle_ReturnsItemsOrderedByIndex()
    {
        // Arrange
        await _addHandler.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );
        await _addHandler.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _otherItem.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var items = await _handler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([_item.Id, _otherItem.Id], items.Select(i => i.ItemId));
    }
}
