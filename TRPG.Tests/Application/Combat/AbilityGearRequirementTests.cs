using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class AbilityGearRequirementTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private CombatantBuilder MakeCombatant() => Builders.NewCombatant().WithWorldId(_worldId);

    private static Ability MakeAbility(string name, GearRequirement gearRequirement) =>
        new AttackAbility
        {
            Name = name,
            GearRequirement = gearRequirement,
            TargetType = AttackTargetType.Single,
        };

    [Fact]
    public void IsMet_ReturnsFalse_WhenShieldIsRequiredButNotEquipped()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Shield Bash", GearRequirement.Shield);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenShieldIsRequiredAndEquipped()
    {
        // Arrange
        var actor = MakeCombatant().WithItem(Builders.MakeShieldItem(_worldId)).Build();
        var ability = MakeAbility("Shield Bash", GearRequirement.Shield);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenMeleeWeaponIsRequiredButNotEquipped()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Slash", GearRequirement.MeleeWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenMeleeWeaponIsRequiredAndEquipped()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Sword);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Slash", GearRequirement.MeleeWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenRangedWeaponIsRequiredButNotEquipped()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Arrow Shot", GearRequirement.RangedWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenRangedWeaponIsRequiredButAMeleeWeaponIsEquipped()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Sword);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Arrow Shot", GearRequirement.RangedWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenRangedWeaponIsRequiredAndEquipped()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Bow);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Arrow Shot", GearRequirement.RangedWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenCasterWeaponIsRequiredButNotEquipped()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Fireball", GearRequirement.CasterWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenCasterWeaponIsRequiredAndEquipped()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Staff);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Fireball", GearRequirement.CasterWeapon);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenNoGearIsRequired()
    {
        // Arrange - Destruction/Illusion abilities in the real AbilityDefinitions never set a
        // GearRequirement, so casting works without a staff/wand equipped.
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Fireball", GearRequirement.None);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }
}
