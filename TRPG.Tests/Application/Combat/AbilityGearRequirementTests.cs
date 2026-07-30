using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class AbilityGearRequirementTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private CombatantBuilder MakeCombatant() => Builders.NewCombatant().WithWorldId(_worldId);

    private static Ability MakeAbility(string name, Skill skill) =>
        new AttackAbility
        {
            Name = name,
            Skill = skill,
            TargetType = AttackTargetType.Single,
        };

    [Fact]
    public void IsMet_ReturnsFalse_WhenShieldBashIsUsedWithoutAShield()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Shield Bash", Skill.Blocking);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenShieldBashIsUsedWithAShield()
    {
        // Arrange
        var actor = MakeCombatant().WithItem(Builders.MakeShieldItem(_worldId)).Build();
        var ability = MakeAbility("Shield Bash", Skill.Blocking);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenMeleeAbilityIsUsedWithoutAMeleeWeapon()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Slash", Skill.Melee);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenMeleeAbilityIsUsedWithAMeleeWeapon()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Sword);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Slash", Skill.Melee);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenArcheryAbilityIsUsedWithoutABow()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Arrow Shot", Skill.Archery);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsFalse_WhenArcheryAbilityIsUsedWithAMeleeWeapon()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Sword);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Arrow Shot", Skill.Archery);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenArcheryAbilityIsUsedWithABow()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(_worldId, type: WeaponType.Bow);
        var actor = MakeCombatant().WithItem(weapon).Build();
        var ability = MakeAbility("Arrow Shot", Skill.Archery);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMet_ReturnsTrue_WhenSpellcastingAbilityIsUsedWithoutAStaffOrWand()
    {
        // Arrange
        var actor = MakeCombatant().Build();
        var ability = MakeAbility("Fireball", Skill.Destruction);

        // Act
        var result = AbilityGearRequirement.IsMet(actor, ability);

        // Assert
        Assert.True(result);
    }
}
