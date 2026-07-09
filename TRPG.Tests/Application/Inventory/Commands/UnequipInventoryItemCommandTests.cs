using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class UnequipInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddInventoryItemCommandHandler _addHandler = null!;
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private Item _item = null!;
    private UnequipInventoryItemCommandHandler _unequipHandler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addHandler = new AddInventoryItemCommandHandler(_context);
        _equipHandler = new EquipInventoryItemCommandHandler(_context);
        _unequipHandler = new UnequipInventoryItemCommandHandler(_context);
        _getHandler = new GetInventoryByCreatureIdQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _item = Builders.MakeItem();

        _context.Creatures.Add(_creature);
        _context.Items.Add(_item);

        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClearsSlot()
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
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _unequipHandler.Handle(
            new UnequipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Null(items.First().EquippedSlot);
    }

    [Fact]
    public async Task Handle_Throws_WhenSlotEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _unequipHandler.Handle(
                new UnequipInventoryItemCommand
                {
                    CreatureId = _creature.Id,
                    Slot = EquipmentSlot.Helm,
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
