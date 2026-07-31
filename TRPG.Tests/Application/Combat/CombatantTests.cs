using TRPG.Application.Combat;
using TRPG.Application.Configuration;
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
            new Dictionary<WeaponType, int>(),
            new CombatOptions()
        );

        // Assert
        Assert.Empty(combatant.ActiveBuffs);
    }

    [Fact]
    public void FromCreature_ExcludesUnequippedItems_FromEquippedItems()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);
        var equippedWeapon = new Weapon
        {
            Name = "Equipped Sword",
            Type = WeaponType.Sword,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.RightHand },
        };
        var unequippedWeapon = new Weapon { Name = "Spare Axe", Type = WeaponType.Axe };
        IReadOnlyList<Item> items = [equippedWeapon, unequippedWeapon];

        // Act
        var combatant = Combatant.FromCreature(
            creature,
            [],
            isPlayer: true,
            items,
            new Dictionary<WeaponType, int>(),
            new CombatOptions()
        );

        // Assert — the unequipped spare never reaches EquippedItems, so Weapon resolves to
        // the one actually worn instead of throwing on more than one Weapon
        Assert.Equal(equippedWeapon.Name, combatant.Weapon!.Name);
    }
}
