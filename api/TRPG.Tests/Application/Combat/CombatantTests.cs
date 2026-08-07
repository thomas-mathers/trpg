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
            new CombatOptions(),
            isPlayer: true,
            creature: creature,
            abilities: [],
            items: [],
            weaponProficiencies: new Dictionary<WeaponType, int>()
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
            new CombatOptions(),
            isPlayer: true,
            creature: creature,
            abilities: [],
            items: items,
            weaponProficiencies: new Dictionary<WeaponType, int>()
        );

        // Assert — the unequipped spare never reaches EquippedItems, so MainHandWeapon resolves
        // to the one actually worn in RightHand
        Assert.Equal(equippedWeapon.Name, combatant.MainHandWeapon!.Name);
    }

    [Fact]
    public void MainHandAndOffHandWeapon_ResolveIndependently_ByEquippedSlot()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);
        var mainHandWeapon = new Weapon
        {
            Name = "Main Dagger",
            Type = WeaponType.Dagger,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.RightHand },
        };
        var offHandWeapon = new Weapon
        {
            Name = "Off Dagger",
            Type = WeaponType.Dagger,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.LeftHand },
        };
        IReadOnlyList<Item> items = [mainHandWeapon, offHandWeapon];

        // Act
        var combatant = Combatant.FromCreature(
            new CombatOptions(),
            isPlayer: true,
            creature: creature,
            abilities: [],
            items: items,
            weaponProficiencies: new Dictionary<WeaponType, int>()
        );

        // Assert
        Assert.Equal(mainHandWeapon.Name, combatant.MainHandWeapon!.Name);
        Assert.Equal(offHandWeapon.Name, combatant.OffHandWeapon!.Name);
    }
}
