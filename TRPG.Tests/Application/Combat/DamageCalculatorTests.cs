using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class DamageCalculatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;

    private static readonly CombatSettings Settings = new()
    {
        StrengthDamageBonusPerPoint = 0.01f,
        IntelligenceDamageBonusPerPoint = 0.01f,
        UnarmedBaseDamage = 3,
    };

    private Combatant MakeCombatant(
        int strength = 0,
        int intelligence = 0,
        IReadOnlyList<Item>? inventory = null
    )
    {
        var creature = Builders.MakeCreature(_worldId);
        creature.Attributes.Strength = strength;
        creature.Attributes.Intelligence = intelligence;
        return Combatant.FromCreature(
            creature,
            [],
            BasicAttack,
            isPlayer: true,
            inventory ?? [],
            []
        );
    }

    private static AttackAbility MakeAttack(
        DamageType damageType = DamageType.Physical,
        float damageAmount = 100
    )
    {
        return new AttackAbility
        {
            Name = "Test Attack",
            Description = "A test attack.",
            TargetType = AttackTargetType.Single,
            DamageType = damageType,
            DamageAmount = damageAmount,
            DamageAmountType = AmountType.Flat,
        };
    }

    private WeaponItem MakeFixedRangeWeapon(int damage)
    {
        return new WeaponItem
        {
            WorldId = _worldId,
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon.",
            Type = WeaponType.Sword,
            MinDamage = damage,
            MaxDamage = damage,
        };
    }

    [Fact]
    public void CalculateDamage_RollsAgainstTheWeapon_ForPhysicalAbilities()
    {
        // Arrange — fixed-range weapon removes the roll, ability at 100% = a plain swing
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = MakeCombatant(inventory: [weapon]);
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(attacker, MakeAttack(damageAmount: 100), defender);

        // Assert
        Assert.Equal(10, damage);
    }

    [Fact]
    public void CalculateDamage_ScalesTheWeaponRoll_ByDamageAmountAsAPercent()
    {
        // Arrange — 10 base × 150% = 15
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = MakeCombatant(inventory: [weapon]);
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(attacker, MakeAttack(damageAmount: 150), defender);

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_UsesTheUnarmedBaseline_WhenNoWeaponIsEquipped()
    {
        // Arrange — no weapon, so UnarmedBaseDamage (3) stands in for the roll: 3 × 100% = 3
        var attacker = MakeCombatant();
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(attacker, MakeAttack(damageAmount: 100), defender);

        // Assert
        Assert.Equal(3, damage);
    }

    [Fact]
    public void CalculateDamage_AppliesStrengthAsAPercentBonus_ForPhysicalAbilities()
    {
        // Arrange — 10 base × 100% × (1 + 50 × 0.01) = 15
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = MakeCombatant(strength: 50, inventory: [weapon]);
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(attacker, MakeAttack(damageAmount: 100), defender);

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_IsSelfContained_ForMagicAbilities()
    {
        // Arrange — magic ignores the weapon entirely
        var weapon = MakeFixedRangeWeapon(999);
        var attacker = MakeCombatant(inventory: [weapon]);
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            MakeAttack(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(20, damage);
    }

    [Fact]
    public void CalculateDamage_AppliesIntelligenceAsAPercentBonus_ForMagicAbilities()
    {
        // Arrange — 20 base × (1 + 50 × 0.01) = 30
        var attacker = MakeCombatant(intelligence: 50);
        var defender = MakeCombatant();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            MakeAttack(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(30, damage);
    }

    [Fact]
    public void CalculateDamage_MitigatesByTheMatchingResistance()
    {
        // Arrange — 20 fire damage, 25% fire resistance = 15
        var attacker = MakeCombatant();
        var defender = MakeCombatant();
        defender.Attributes.FireResistance = 0.25f;
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            MakeAttack(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(15, damage);
    }

    [Fact]
    public void CalculateDamage_NeverGoesBelowZero_WhenResistanceExceedsTheRawAmount()
    {
        // Arrange
        var attacker = MakeCombatant();
        var defender = MakeCombatant();
        defender.Attributes.FireResistance = 1.5f;
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            MakeAttack(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(0, damage);
    }

    [Fact]
    public void CalculateDamage_MitigatesARawAmount_ForDotTicks()
    {
        // Arrange — the DoT-tick overload skips weapon/attribute resolution entirely
        var defender = MakeCombatant();
        defender.Attributes.PoisonResistance = 0.5f;
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(10, DamageType.Poison, defender);

        // Assert
        Assert.Equal(5, damage);
    }
}
