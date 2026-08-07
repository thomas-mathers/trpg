using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class UnequipInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private UnequipInventoryItemCommandHandler _unequipHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Item _item = Builders.MakeWeaponItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _equipHandler = new EquipInventoryItemCommandHandler(_context);
        _unequipHandler = new UnequipInventoryItemCommandHandler(_context);
        _getHandler = new GetInventoryByCreatureIdQueryHandler(_context);

        _item.Quantity = 1;
        _item.Ownership.OwnerId = _creature.Id;
        _item.Ownership.OwnerType = OwnerType.Creature;

        _context.Creatures.Add(_creature);
        _context.Items.Add(_item);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClearsSlot()
    {
        // Arrange
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
        Assert.Null(items[0].Ownership.EquippedSlot);
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

    [Fact]
    public async Task Handle_ClampsCurrentHpDown_WhenUnequippingReducesMaximum()
    {
        // Arrange
        var baseMaximumHp = _creature.MaximumHp;
        var gear = Builders.MakeArmorItem(
            worldId: _creature.WorldId,
            modifiers:
            [
                new AttributeModifier
                {
                    Attribute = AttributeName.MaximumHp,
                    Amount = 50,
                    AmountType = AmountType.Flat,
                },
            ]
        );
        gear.Quantity = 1;
        gear.Ownership.OwnerId = _creature.Id;
        gear.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gear);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = gear.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );
        _creature.CurrentHp = _creature.MaximumHp;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _unequipHandler.Handle(
            new UnequipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(baseMaximumHp, updated!.MaximumHp);
        Assert.Equal(baseMaximumHp, updated.CurrentHp);
    }
}
