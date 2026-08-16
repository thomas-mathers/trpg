using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;
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
                IntelligenceDamageLogDivisor = 50f,
                MaxResistancePercent = 0.75f,
                // Zeroed so CalculateDamage's real random crit roll can't turn an exact-value
                // assertion flaky - crit behavior itself gets its own dedicated test instead.
                CritChancePerDexterityPoint = 0f,
            }
        );

    private Weapon MakeFixedRangeWeapon(int damage) =>
        Builders.MakeWeaponItem(worldId: _worldId, minDamage: damage, maxDamage: damage);

    [Fact]
    public void CalculateDamage_RollsAgainstTheWeapon_ForPhysicalAbilities()
    {
        // Arrange — fixed-range weapon removes the roll, ability at 100% = a plain swing
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithCombatOptions(Settings.Value)
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
        Assert.Equal(10, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_ScalesTheWeaponRoll_ByDamageAmountAsAPercent()
    {
        // Arrange — 10 base × 150% = 15
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 150),
            defender
        );

        // Assert
        Assert.Equal(15, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_UsesTheNaturalWeaponRoll_WhenAttackerIsDisarmed()
    {
        // Arrange — a real 10-damage weapon is equipped, but Disarmed forces the fallback to the
        // natural-weapon roll instead (fixed at 3-3 by the builder's default): 3 x 100% = 3
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithCondition(ConditionType.Disarmed, 3)
            .WithCombatOptions(Settings.Value)
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
        Assert.Equal(3, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_UsesTheUnarmedBaseline_WhenNoWeaponIsEquipped()
    {
        // Arrange — no weapon, so the combatant's natural-weapon range (fixed at 3-3 by the
        // builder's default) stands in for the roll: 3 × 100% = 3
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithCombatOptions(Settings.Value)
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
        Assert.Equal(3, damage.Amount);
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
            .WithCombatOptions(Settings.Value)
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
        Assert.Equal(15, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_AddsElementalWeaponDamage_AfterTheMatchingResistance()
    {
        // Arrange â€” 10 physical damage plus 8 fixed fire damage against 25% fire resistance.
        var weapon = Builders.MakeWeaponItem(
            worldId: _worldId,
            minDamage: 10,
            maxDamage: 10,
            modifiers:
            [
                new ElementalDamageModifier
                {
                    DamageType = DamageType.Fire,
                    MinDamage = 8,
                    MaxDamage = 8,
                },
            ]
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithFireResistance(0.25f)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 100),
            defender
        );

        // Assert
        Assert.Equal(16, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_IsSelfContained_ForMagicAbilities()
    {
        // Arrange — magic ignores the weapon entirely
        var weapon = MakeFixedRangeWeapon(999);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(20, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_AppliesIntelligenceAsALogarithmicBonus_ForMagicAbilities()
    {
        // Arrange — 20 base × (1 + ln(1 + 50 / 50)) = 20 × (1 + ln(2)) ≈ 33
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithIntelligence(50)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(33, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_MitigatesByTheMatchingResistance()
    {
        // Arrange — 20 fire damage, 25% fire resistance = 15
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithCombatOptions(Settings.Value)
            .Build();
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
        Assert.Equal(15, damage.Amount);
    }

    [Fact]
    public void CalculateDamage_ClampsResistanceAtMaxResistancePercent_WhenResistanceExceedsTheCap()
    {
        // Arrange — 20 fire damage, resistance rolled at 150% but clamped to the 75% cap = 5
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithCombatOptions(Settings.Value)
            .Build();
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
        Assert.Equal(5, damage.Amount);
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

    [Fact]
    public void CalculateDamage_ReportsIsCriticalFalse_WhenCritChanceIsZero()
    {
        // Arrange — Settings zeroes CritChancePerDexterityPoint, so the roll can never succeed
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithCombatOptions(Settings.Value)
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
        Assert.False(damage.IsCritical);
    }

    [Fact]
    public void CalculateDamage_AppliesCritDamageMultiplier_WhenCritChanceRollAlwaysSucceeds()
    {
        // Arrange — 100 Dexterity x 1 = 100% crit chance, guaranteeing the roll succeeds;
        // 10 base x 2x crit multiplier = 20
        var settings = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                CritChancePerDexterityPoint = 1f,
                MaxCritChance = 1f,
                CritDamageMultiplier = 2f,
            }
        );
        var weapon = MakeFixedRangeWeapon(10);
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDexterity(100)
            .WithItem(weapon)
            .WithCombatOptions(settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(settings);

        // Act
        var damage = calculator.CalculateDamage(
            attacker,
            Builders.MakeAttackAbility(damageAmount: 100),
            defender
        );

        // Assert
        Assert.True(damage.IsCritical);
        Assert.Equal(20, damage.Amount);
    }

    [Fact]
    public void EstimateDamage_AppliesExpectedCritBonus_BasedOnDexterity()
    {
        // Arrange — 20 base magic damage, crit chance 100 x 0.01 = 100% capped at 50%; the
        // expected-value blend is 20 x (1 + 0.5 x (2 - 1)) = 30
        var settings = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                CritChancePerDexterityPoint = 0.01f,
                MaxCritChance = 0.5f,
                CritDamageMultiplier = 2f,
            }
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDexterity(100)
            .WithCombatOptions(settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(settings);

        // Act
        var damage = calculator.EstimateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(30, damage);
    }

    [Fact]
    public void EstimateDamage_ReducesCritChanceContribution_WhenAttackerHasASnareDebuff()
    {
        // Arrange — same setup as the crit test above, but a snare debuff halves the 100
        // Dexterity to 50, halving crit chance to 50% x 0.01 = 50% (still capped, unaffected) -
        // use a lower Dexterity so the halving actually changes the capped outcome: 60 x 0.01 =
        // 60% capped to 50% undebuffed (full multiplier); debuffed, 30 x 0.01 = 30% (partial):
        // 20 x (1 + 0.3) = 26
        var settings = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                CritChancePerDexterityPoint = 0.01f,
                MaxCritChance = 0.5f,
                CritDamageMultiplier = 2f,
            }
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDexterity(60)
            .WithActiveBuff(AttributeName.Dexterity, -50, AmountType.Percent, remainingTurns: 2)
            .WithCombatOptions(settings.Value)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new DamageCalculator(settings);

        // Act
        var damage = calculator.EstimateDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20),
            defender
        );

        // Assert
        Assert.Equal(26, damage);
    }

    [Fact]
    public void EstimateRawDamage_IgnoresDefenderEntirely_UnlikeEstimateDamage()
    {
        // Arrange — 20 fire damage; EstimateRawDamage has no defender to mitigate against
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damage = calculator.EstimateRawDamage(
            attacker,
            Builders.MakeAttackAbility(damageType: DamageType.Fire, damageAmount: 20)
        );

        // Assert
        Assert.Equal(20, damage);
    }

    [Fact]
    public void EstimateBasicAttackDamagePerTurn_MultipliesByMainHandAttacksPerTurn()
    {
        // Arrange — fixed 10-damage weapon, 3 attacks per turn = 30
        var weapon = Builders.MakeWeaponItem(
            worldId: _worldId,
            minDamage: 10,
            maxDamage: 10,
            attacksPerTurn: 3
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon, slot: EquipmentSlot.RightHand)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damagePerTurn = calculator.EstimateBasicAttackDamagePerTurn(attacker);

        // Assert
        Assert.Equal(30, damagePerTurn);
    }

    [Fact]
    public void EstimateBasicAttackDamagePerTurn_IncludesAverageElementalWeaponDamage()
    {
        // Arrange — a 10-damage weapon with a 4-8 fire affix has 16 expected damage per swing.
        var weapon = Builders.MakeWeaponItem(
            worldId: _worldId,
            minDamage: 10,
            maxDamage: 10,
            modifiers:
            [
                new ElementalDamageModifier
                {
                    DamageType = DamageType.Fire,
                    MinDamage = 4,
                    MaxDamage = 8,
                },
            ]
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon, slot: EquipmentSlot.RightHand)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damagePerTurn = calculator.EstimateBasicAttackDamagePerTurn(attacker);

        // Assert
        Assert.Equal(16, damagePerTurn);
    }

    [Fact]
    public void EstimateBasicAttackDamagePerTurn_AddsOffHandWeaponSwings()
    {
        // Arrange — main hand 10x1 + off hand 5x2 = 20
        var mainHand = Builders.MakeWeaponItem(worldId: _worldId, minDamage: 10, maxDamage: 10);
        var offHand = Builders.MakeWeaponItem(
            worldId: _worldId,
            minDamage: 5,
            maxDamage: 5,
            attacksPerTurn: 2
        );
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(mainHand, slot: EquipmentSlot.RightHand)
            .WithItem(offHand, slot: EquipmentSlot.LeftHand)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damagePerTurn = calculator.EstimateBasicAttackDamagePerTurn(attacker);

        // Assert
        Assert.Equal(20, damagePerTurn);
    }

    [Fact]
    public void EstimateBasicAttackDamagePerTurn_UsesNaturalWeaponDamage_WhenUnarmed()
    {
        // Arrange — no weapon equipped, natural weapon fixed at 4-4
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithNaturalWeaponDamage(4, 4)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new DamageCalculator(Settings);

        // Act
        var damagePerTurn = calculator.EstimateBasicAttackDamagePerTurn(attacker);

        // Assert
        Assert.Equal(4, damagePerTurn);
    }
}
