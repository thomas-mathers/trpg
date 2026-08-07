using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class EquipInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Item _weapon = Builders.MakeWeaponItem();
    private readonly Item _otherWeapon = Builders.MakeWeaponItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _equipHandler = new EquipInventoryItemCommandHandler(_context);
        _getHandler = new GetInventoryByCreatureIdQueryHandler(_context);

        GiveToCreature(_weapon);
        GiveToCreature(_otherWeapon);

        _context.Creatures.Add(_creature);
        _context.Items.AddRange(_weapon, _otherWeapon);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private void GiveToCreature(Item item)
    {
        item.Quantity = 1;
        item.Ownership.OwnerId = _creature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
    }

    [Fact]
    public async Task Handle_SetsEquippedSlot()
    {
        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            EquipmentSlot.RightHand,
            items.First(i => i.Id == _weapon.Id).Ownership.EquippedSlot
        );
    }

    [Fact]
    public async Task Handle_UnequipsPrevious_WhenSlotAlreadyOccupied()
    {
        // Arrange
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _otherWeapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        var equipped = items
            .Where(i => i.Ownership.EquippedSlot == EquipmentSlot.RightHand)
            .ToList();

        Assert.Single(equipped);
        Assert.Equal(_otherWeapon.Id, equipped[0].Id);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureCachedAttributes_WhenGearIsEquipped()
    {
        // Arrange
        var baseDefense = _creature.Defense;
        var armor = Builders.MakeArmorItem(worldId: _creature.WorldId);
        GiveToCreature(armor);
        _context.Items.Add(armor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = armor.Id,
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
        Assert.Equal(baseDefense + armor.Defense, updated!.Defense);
    }

    [Fact]
    public async Task Handle_Throws_WhenItemIsNotEquippable()
    {
        // Arrange
        var potion = Builders.MakeConsumableItem(worldId: _creature.WorldId);
        GiveToCreature(potion);
        _context.Items.Add(potion);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _equipHandler.Handle(
                new EquipInventoryItemCommand
                {
                    CreatureId = _creature.Id,
                    ItemId = potion.Id,
                    Slot = EquipmentSlot.RightHand,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ClearsBothHandSlots_WhenEquippingTwoHandedWeapon()
    {
        // Arrange
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _otherWeapon.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );
        var greatSword = Builders.MakeWeaponItem(worldId: _creature.WorldId, isTwoHanded: true);
        GiveToCreature(greatSword);
        _context.Items.Add(greatSword);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = greatSword.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            EquipmentSlot.RightHand,
            items.First(i => i.Id == greatSword.Id).Ownership.EquippedSlot
        );
        Assert.Null(items.First(i => i.Id == _weapon.Id).Ownership.EquippedSlot);
        Assert.Null(items.First(i => i.Id == _otherWeapon.Id).Ownership.EquippedSlot);
    }

    [Fact]
    public async Task Handle_ResolvesTwoHandedWeaponToRightHand_WhenLeftHandRequested()
    {
        // Arrange
        var greatAxe = Builders.MakeWeaponItem(worldId: _creature.WorldId, isTwoHanded: true);
        GiveToCreature(greatAxe);
        _context.Items.Add(greatAxe);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = greatAxe.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            EquipmentSlot.RightHand,
            items.First(i => i.Id == greatAxe.Id).Ownership.EquippedSlot
        );
    }

    [Fact]
    public async Task Handle_UnequipsTwoHandedWeapon_WhenEquippingOffHandItem()
    {
        // Arrange
        var greatHammer = Builders.MakeWeaponItem(worldId: _creature.WorldId, isTwoHanded: true);
        GiveToCreature(greatHammer);
        _context.Items.Add(greatHammer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = greatHammer.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _otherWeapon.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            EquipmentSlot.LeftHand,
            items.First(i => i.Id == _otherWeapon.Id).Ownership.EquippedSlot
        );
        Assert.Null(items.First(i => i.Id == greatHammer.Id).Ownership.EquippedSlot);
    }
}
