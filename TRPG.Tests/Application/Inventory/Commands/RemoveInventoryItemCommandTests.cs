using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class RemoveInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddInventoryItemCommandHandler _addHandler = null!;
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private Item _item = null!;
    private RemoveInventoryItemCommandHandler _removeHandler = null!;
    private Item _stackableItem = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addHandler = new AddInventoryItemCommandHandler(_context);
        _removeHandler = new RemoveInventoryItemCommandHandler(_context);
        _getHandler = new GetInventoryByCreatureIdQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _item = Builders.MakeItem();
        _stackableItem = Builders.MakeConsumableItem();

        _context.Creatures.Add(_creature);
        _context.Items.Add(_item);
        _context.Items.Add(_stackableItem);

        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DecreasesQuantity()
    {
        // Arrange
        await _addHandler.Handle(
            new AddInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _stackableItem.Id,
                Quantity = 5,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _stackableItem.Id,
                Quantity = 3,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(2, items.First().Quantity);
    }

    [Fact]
    public async Task Handle_RemovesEntry_WhenQuantityReachesZero()
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

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(items);
    }

    [Fact]
    public async Task Handle_Throws_WhenItemNotInInventory()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _removeHandler.Handle(
                new RemoveInventoryItemCommand
                {
                    CreatureId = _creature.Id,
                    ItemId = _item.Id,
                    Quantity = 1,
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
