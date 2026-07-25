using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class DamageCalculatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> Settings =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                StrengthDamageBonusPerPoint = 0.01f,
                IntelligenceDamageBonusPerPoint = 0.01f,
                UnarmedBaseDamage = 3,
            }
        );

    private WeaponItem MakeFixedRangeWeapon(int damage) =>
        Builders.MakeWeaponItem(worldId: _worldId, minDamage: damage, maxDamage: damage);

    [Fact]
    public void CalculateDamage_RollsAgainstTheWeapon_ForPhysicalAbilities()
    {
        // Arrange — fixed-range weapon removes the roll, ability at 100% = a plain swing
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).WithItem(weapon).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 100),
            defender
        );

        // Assert
        Assert.Equal(10, damage);
    }

    [Fact]
    public void CalculateDamage_ScalesTheWeaponRoll_ByDamageAmountAsAPercent()
    {
        // Arrange — 10 base × 150% = 15
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).WithItem(weapon).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 150),
            defender
        );

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_UsesTheUnarmedBaseline_WhenNoWeaponIsEquipped()
    {
        // Arrange — no weapon, so UnarmedBaseDamage (3) stands in for the roll: 3 × 100% = 3
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 100),
            defender
        );

        // Assert
        Assert.Equal(3, damage);
    }

    [Fact]
    public void CalculateDamage_AppliesStrengthAsAPercentBonus_ForPhysicalAbilities()
    {
        // Arrange — 10 base × 100% × (1 + 50 × 0.01) = 15
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithStrength(50)
            .WithItem(weapon)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 100),
            defender
        );

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_IsSelfContained_ForMagicAbilities()
    {
        // Arrange — magic ignores the weapon entirely
        var weapon = MakeFixedRangeWeapon(999);
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).WithItem(weapon).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(20, damage);
    }

    [Fact]
    public void CalculateDamage_AppliesIntelligenceAsAPercentBonus_ForMagicAbilities()
    {
        // Arrange — 20 base × (1 + 50 × 0.01) = 30
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).WithIntelligence(50).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(30, damage);
    }

    [Fact]
    public void CalculateDamage_MitigatesByTheMatchingResistance()
    {
        // Arrange — 20 fire damage, 25% fire resistance = 15
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithFireResistance(0.25f)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_NeverGoesBelowZero_WhenResistanceExceedsTheRawAmount()
    {
        // Arrange
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithFireResistance(1.5f)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(0, damage);
    }

    [Fact]
    public void CalculateDamage_MitigatesARawAmount_ForDotTicks()
    {
        // Arrange — the DoT-tick overload skips weapon/attribute resolution entirely
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithPoisonResistance(0.5f)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(10, DamageType.Poison, defender);

        // Assert
        Assert.Equal(5, damage);
    }
}
