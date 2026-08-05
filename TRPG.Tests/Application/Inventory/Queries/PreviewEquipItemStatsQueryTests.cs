using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class PreviewEquipItemStatsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PreviewEquipItemStatsQueryHandler _previewHandler = null!;
    private EquipInventoryItemCommandHandler _equipHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Armor _armor = Builders.MakeArmorItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _previewHandler = new PreviewEquipItemStatsQueryHandler(_context);
        _equipHandler = new EquipInventoryItemCommandHandler(_context);

        GiveToCreature(_armor);

        _context.Creatures.Add(_creature);
        _context.Items.Add(_armor);
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
    public async Task Handle_ReturnsHigherDefense_WhenPreviewingUnequippedArmor()
    {
        // Act
        var stats = await _previewHandler.Handle(
            new PreviewEquipItemStatsQuery
            {
                CreatureId = _creature.Id,
                ItemId = _armor.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_creature.Defense + _armor.Defense, stats.Defense);
    }

    [Fact]
    public async Task Handle_ExcludesCurrentlyEquippedItemInSameSlot_WhenPreviewingReplacement()
    {
        // Arrange
        var baseDefense = _creature.Defense;
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _armor.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );
        var replacementArmor = Builders.MakeArmorItem(worldId: _creature.WorldId);
        GiveToCreature(replacementArmor);
        _context.Items.Add(replacementArmor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var stats = await _previewHandler.Handle(
            new PreviewEquipItemStatsQuery
            {
                CreatureId = _creature.Id,
                ItemId = replacementArmor.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(baseDefense + replacementArmor.Defense, stats.Defense);
    }

    [Fact]
    public async Task Handle_ExcludesConflictingItems_WhenPreviewingTwoHandedWeapon()
    {
        // Arrange
        var baseDefense = _creature.Defense;
        var shield = Builders.MakeShieldItem(worldId: _creature.WorldId);
        var weapon = Builders.MakeWeaponItem(worldId: _creature.WorldId);
        var greatSword = Builders.MakeWeaponItem(worldId: _creature.WorldId, isTwoHanded: true);
        GiveToCreature(shield);
        GiveToCreature(weapon);
        GiveToCreature(greatSword);
        _context.Items.AddRange(shield, weapon, greatSword);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = weapon.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );
        await _equipHandler.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = shield.Id,
                Slot = EquipmentSlot.LeftHand,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var stats = await _previewHandler.Handle(
            new PreviewEquipItemStatsQuery
            {
                CreatureId = _creature.Id,
                ItemId = greatSword.Id,
                Slot = EquipmentSlot.RightHand,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(baseDefense, stats.Defense);
    }

    [Fact]
    public async Task Handle_DoesNotPersistAnyChanges()
    {
        // Act
        await _previewHandler.Handle(
            new PreviewEquipItemStatsQuery
            {
                CreatureId = _creature.Id,
                ItemId = _armor.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        var updatedArmor = await verifyContext.Items.FindAsync(
            [_armor.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.Defense, updatedCreature!.Defense);
        Assert.Null(updatedArmor!.Ownership.EquippedSlot);
    }
}
