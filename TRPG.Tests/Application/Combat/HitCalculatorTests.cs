using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class HitCalculatorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> Settings =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                BaseProficiency = 0,
                MinHitChance = 0.05f,
                MaxHitChance = 0.95f,
            }
        );

    private static readonly IOptionsSnapshot<CombatOptions> BaseProficiencySettings =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                BaseProficiency = 50,
                MinHitChance = 0.05f,
                MaxHitChance = 0.95f,
            }
        );

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysHit =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 1.0f, MaxHitChance = 1.0f }
        );

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysMiss =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 0.0f, MaxHitChance = 0.0f }
        );

    private Weapon MakeWeapon(WeaponType type = WeaponType.Sword) =>
        Builders.MakeWeaponItem(worldId: _worldId, type: type);

    [Fact]
    public void CalculateHitChance_IsTheProficiencyShareOfTheCombinedTotal()
    {
        // Arrange — proficiency 30 vs defense 70 = 30%
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(70).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.3f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_ClampsToMinHitChance_WhenTheRatioIsTooLow()
    {
        // Arrange — proficiency 1 vs defense 999 would ratio to well under the floor
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 1)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(999).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(Settings.Value.MinHitChance, chance);
    }

    [Fact]
    public void CalculateHitChance_ClampsToMaxHitChance_WhenTheRatioIsTooHigh()
    {
        // Arrange — proficiency 999 vs defense 1 would ratio to well over the ceiling
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 999)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(1).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(Settings.Value.MaxHitChance, chance);
    }

    [Fact]
    public void CalculateHitChance_FallsBackToMinHitChance_WhenProficiencyAndDefenseAreBothZero()
    {
        // Arrange — a fresh weapon type against an unarmored target: 0 / 0 would otherwise be NaN
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 0)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(0).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(Settings.Value.MinHitChance, chance);
    }

    [Fact]
    public void CalculateHitChance_UsesBaseProficiency_WhenUnarmed()
    {
        // Arrange — unarmed relies on the base proficiency alone: 50 vs defense 50 = 50%
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(50).Build();
        var calculator = new HitCalculator(BaseProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.5f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_AddsBaseProficiency_EvenForAnUntrainedWeapon()
    {
        // Arrange — a freshly-equipped, never-swung weapon (0 trained) must be no worse than unarmed
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 0)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(50).Build();
        var calculator = new HitCalculator(BaseProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.5f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_AddsTrainedProficiency_OnTopOfTheBase()
    {
        // Arrange — base 50 + trained 30 = 80 vs defense 20 = 80%
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(20).Build();
        var calculator = new HitCalculator(BaseProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.8f, chance, precision: 3);
    }

    [Fact]
    public void DidHit_AlwaysHits_ForNonPhysicalAbilities()
    {
        // Arrange — settings that would always miss a physical roll
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(999).Build();
        var calculator = new HitCalculator(AlwaysMiss);

        // Act
        var didHit = calculator.DidHit(
            attacker,
            Builders.MakeAttackAbility(DamageType.Fire),
            defender
        );

        // Assert
        Assert.True(didHit);
    }

    [Fact]
    public void DidHit_RespectsTheClampedChance_ForPhysicalAbilities()
    {
        // Arrange
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var alwaysHitCalculator = new HitCalculator(AlwaysHit);
        var alwaysMissCalculator = new HitCalculator(AlwaysMiss);

        // Act & Assert
        Assert.True(alwaysHitCalculator.DidHit(attacker, Builders.MakeAttackAbility(), defender));
        Assert.False(alwaysMissCalculator.DidHit(attacker, Builders.MakeAttackAbility(), defender));
    }

    [Fact]
    public void DidBlock_NeverBlocks_ForNonPhysicalAbilities()
    {
        // Arrange — a shield that would otherwise always block
        var shield = Builders.MakeShieldItem(worldId: _worldId, blockChance: 1.0f);
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithItem(shield).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var didBlock = calculator.DidBlock(Builders.MakeAttackAbility(DamageType.Fire), defender);

        // Assert
        Assert.False(didBlock);
    }

    [Fact]
    public void DidBlock_RollsAgainstBlockChance_ForPhysicalAbilities()
    {
        // Arrange
        var blockingShield = Builders.MakeShieldItem(worldId: _worldId, blockChance: 1.0f);
        var blockingDefender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithItem(blockingShield)
            .Build();
        var unshieldedDefender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var calculator = new HitCalculator(Settings);

        // Act & Assert
        Assert.True(calculator.DidBlock(Builders.MakeAttackAbility(), blockingDefender));
        Assert.False(calculator.DidBlock(Builders.MakeAttackAbility(), unshieldedDefender));
    }
}
