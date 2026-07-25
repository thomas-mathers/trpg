using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatantTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void FromCreature_StartsWithNoActiveModifiers_SinceBuffsAreCombatScoped()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);

        // Act
        var combatant = Combatant.FromCreature(
            creature,
            [],
            isPlayer: true,
            [],
            new Dictionary<WeaponType, int>()
        );

        // Assert
        Assert.Empty(combatant.ActiveBuffs);
    }

    [Fact]
    public void FromCreature_ExcludesUnequippedItems_FromEquippedItems()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);
        var equippedWeapon = new WeaponItem { Name = "Equipped Sword", Type = WeaponType.Sword };
        var unequippedWeapon = new WeaponItem { Name = "Spare Axe", Type = WeaponType.Axe };
        IReadOnlyList<InventoryItem> inventoryItems =
        [
            new InventoryItem { Item = equippedWeapon, EquippedSlot = EquipmentSlot.RightHand },
            new InventoryItem { Item = unequippedWeapon, EquippedSlot = null },
        ];

        // Act
        var combatant = Combatant.FromCreature(
            creature,
            [],
            isPlayer: true,
            inventoryItems,
            new Dictionary<WeaponType, int>()
        );

        // Assert — the unequipped spare never reaches EquippedItems, so Weapon resolves to
        // the one actually worn instead of throwing on more than one WeaponItem
        Assert.Equal(equippedWeapon.Name, combatant.Weapon!.Name);
    }
}
