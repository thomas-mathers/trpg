using Microsoft.Extensions.Options;
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

    private static readonly IOptionsSnapshot<CombatOptions> NonPlayerProficiencySettings =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                NonPlayerProficiencyBase = 100,
                NonPlayerProficiencyPerLevel = 10,
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
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(70)
            .WithDexterity(0)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.3f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_AddsDexterityAsEvasion_OnTopOfDefense()
    {
        // Arrange — proficiency 30 vs (defense 0 + dexterity 100 x 0.6 evasion) = 30 vs 60 = 33.3%
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(0)
            .WithDexterity(100)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.333f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_HalvesEvasionContribution_WhenDefenderHasASnareDebuff()
    {
        // Arrange — proficiency 30 vs (defense 0 + dexterity 100 x 0.5 snare debuff x 0.6 evasion
        // = 30) = 30 vs 30 = 50%, instead of the 33.3% an undebuffed defender would get (see the
        // AddsDexterityAsEvasion test above)
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(0)
            .WithDexterity(100)
            .WithActiveBuff(AttributeName.Dexterity, -50, AmountType.Percent, remainingTurns: 2)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.5f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_ClampsToMinHitChance_WhenTheRatioIsTooLow()
    {
        // Arrange — proficiency 1 vs defense 999 would ratio to well under the floor
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 1)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(999)
            .WithCombatOptions(Settings.Value)
            .Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(Settings.Value.MinHitChance, chance);
    }

    [Fact]
    public void CalculateHitChance_ClampsToMinHitChance_WhenDefenderDexterityIsExtreme()
    {
        // Arrange — proficiency 1 vs an absurd amount of evasion would ratio to well under the
        // floor; no amount of Dexterity should be able to push hit chance to zero
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 1)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDexterity(999_999)
            .WithCombatOptions(Settings.Value)
            .Build();
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
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 999)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(1)
            .WithCombatOptions(Settings.Value)
            .Build();
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
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 0)
            .WithCombatOptions(Settings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(0)
            .WithDexterity(0)
            .WithCombatOptions(Settings.Value)
            .Build();
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
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(50)
            .WithDexterity(0)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
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
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 0)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(50)
            .WithDexterity(0)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
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
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 30)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(20)
            .WithDexterity(0)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var calculator = new HitCalculator(BaseProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.8f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_UsesTheSuppliedWeaponsProficiency_NotMainHands()
    {
        // Arrange — main hand trained 200 (deliberately huge), off hand trained 30; passing the
        // off-hand weapon explicitly must use its own 30, not the main hand's 200
        // (base 50 + 30 vs defense 20 = 80%; using main hand's proficiency instead would clamp
        // to MaxHitChance 95%)
        var mainHandWeapon = MakeWeapon(WeaponType.Dagger);
        var offHandWeapon = MakeWeapon(WeaponType.Sword);
        var attacker = Builders
            .NewCombatant()
            .AsPlayer()
            .WithWorldId(_worldId)
            .WithItem(mainHandWeapon)
            .WithItem(offHandWeapon, slot: EquipmentSlot.LeftHand)
            .WithWeaponProficiency(mainHandWeapon.Type, 200)
            .WithWeaponProficiency(offHandWeapon.Type, 30)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(20)
            .WithDexterity(0)
            .WithCombatOptions(BaseProficiencySettings.Value)
            .Build();
        var calculator = new HitCalculator(BaseProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender, offHandWeapon);

        // Assert
        Assert.Equal(0.8f, chance, precision: 3);
    }

    [Fact]
    public void CalculateHitChance_IgnoresWeaponProficiency_ForNonPlayerCombatants()
    {
        // Arrange — level 1: formulaic proficiency 100 + 10 = 110 vs defense 10 = 92%, regardless
        // of the (deliberately huge) WeaponProficiency dict value below
        var weapon = MakeWeapon();
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithLevel(1)
            .WithItem(weapon)
            .WithWeaponProficiency(weapon.Type, 999_999)
            .WithCombatOptions(NonPlayerProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(10)
            .WithDexterity(0)
            .WithCombatOptions(NonPlayerProficiencySettings.Value)
            .Build();
        var calculator = new HitCalculator(NonPlayerProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.92f, chance, precision: 2);
    }

    [Fact]
    public void CalculateHitChance_ScalesFormulaicProficiency_ByLevel_ForNonPlayerCombatants()
    {
        // Arrange — level 10: proficiency 100 + 10 × 10 = 200 vs defense 200 = 50%
        var attacker = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithLevel(10)
            .WithCombatOptions(NonPlayerProficiencySettings.Value)
            .Build();
        var defender = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithDefense(200)
            .WithDexterity(0)
            .WithCombatOptions(NonPlayerProficiencySettings.Value)
            .Build();
        var calculator = new HitCalculator(NonPlayerProficiencySettings);

        // Act
        var chance = calculator.CalculateHitChance(attacker, defender);

        // Assert
        Assert.Equal(0.5f, chance, precision: 3);
    }

    [Fact]
    public void RollHit_AlwaysHits_ForNonPhysicalAbilities()
    {
        // Arrange — settings that would always miss a physical roll
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithDefense(999).Build();
        var calculator = new HitCalculator(AlwaysMiss);

        // Act
        var didHit = calculator.RollHit(
            attacker,
            Builders.MakeAttackAbility(DamageType.Fire),
            defender
        );

        // Assert
        Assert.True(didHit);
    }

    [Fact]
    public void RollHit_RespectsTheClampedChance_ForPhysicalAbilities()
    {
        // Arrange
        var attacker = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var defender = Builders.NewCombatant().WithWorldId(_worldId).Build();
        var alwaysHitCalculator = new HitCalculator(AlwaysHit);
        var alwaysMissCalculator = new HitCalculator(AlwaysMiss);

        // Act & Assert
        Assert.True(alwaysHitCalculator.RollHit(attacker, Builders.MakeAttackAbility(), defender));
        Assert.False(
            alwaysMissCalculator.RollHit(attacker, Builders.MakeAttackAbility(), defender)
        );
    }

    [Fact]
    public void RollBlock_NeverBlocks_ForNonPhysicalAbilities()
    {
        // Arrange — a shield that would otherwise always block
        var shield = Builders.MakeShieldItem(worldId: _worldId, blockChance: 1.0f);
        var defender = Builders.NewCombatant().WithWorldId(_worldId).WithItem(shield).Build();
        var calculator = new HitCalculator(Settings);

        // Act
        var didBlock = calculator.RollBlock(Builders.MakeAttackAbility(DamageType.Fire), defender);

        // Assert
        Assert.False(didBlock);
    }

    [Fact]
    public void RollBlock_RollsAgainstBlockChance_ForPhysicalAbilities()
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
        Assert.True(calculator.RollBlock(Builders.MakeAttackAbility(), blockingDefender));
        Assert.False(calculator.RollBlock(Builders.MakeAttackAbility(), unshieldedDefender));
    }
}
