using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class EquipInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddInventoryItemCommandHandler _addHandler = null!;
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private Item _item = null!;
    private Item _stackableItem = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addHandler = new AddInventoryItemCommandHandler(_context);
        _equipHandler = new EquipInventoryItemCommandHandler(_context);
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
    public async Task Handle_SetsEquippedSlot()
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
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EquipmentSlot.RightHand, items.First().EquippedSlot);
    }

    [Fact]
    public async Task Handle_UnequipsPrevious_WhenSlotAlreadyOccupied()
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
                ItemId = _stackableItem.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _stackableItem.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        var equipped = items.Where(i => i.EquippedSlot == EquipmentSlot.RightHand).ToList();

        Assert.Single(equipped);
        Assert.Equal(_stackableItem.Id, equipped[0].ItemId);
    }
}
