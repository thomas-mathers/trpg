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
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;

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

    private static AttackAbility MakeAttack(DamageType damageType = DamageType.Physical)
    {
        return new AttackAbility
        {
            Name = "Test Attack",
            Description = "A test attack.",
            TargetType = AttackTargetType.Single,
            DamageType = damageType,
            DamageAmount = 100,
            DamageAmountType = AmountType.Flat,
        };
    }

    private Combatant MakeCombatant(
        int defense = 0,
        WeaponItem? weapon = null,
        int? proficiency = null
    )
    {
        var creature = Builders.MakeCreature(_worldId);
        creature.Attributes.Defense = defense;
        var inventory = weapon != null ? new Item[] { weapon } : [];
        var proficiencies =
            weapon != null && proficiency != null
                ? new Dictionary<WeaponType, int> { [weapon.Type] = proficiency.Value }
                : new Dictionary<WeaponType, int>();
        return Combatant.FromCreature(
            creature,
            [],
            BasicAttack,
            isPlayer: true,
            inventory,
            proficiencies
        );
    }

    private static WeaponItem MakeWeapon(Guid worldId, WeaponType type = WeaponType.Sword)
    {
        return new WeaponItem
        {
            WorldId = worldId,
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon.",
            Type = type,
            MinDamage = 5,
            MaxDamage = 10,
        };
    }

    [Fact]
    public void CalculateHitChance_IsTheProficiencyShareOfTheCombinedTotal()
    {
        // Arrange — proficiency 30 vs defense 70 = 30%
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 30);
        var defender = MakeCombatant(defense: 70);
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
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 1);
        var defender = MakeCombatant(defense: 999);
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
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 999);
        var defender = MakeCombatant(defense: 1);
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
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 0);
        var defender = MakeCombatant(defense: 0);
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
        var attacker = MakeCombatant();
        var defender = MakeCombatant(defense: 50);
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
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 0);
        var defender = MakeCombatant(defense: 50);
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
        var weapon = MakeWeapon(_worldId);
        var attacker = MakeCombatant(weapon: weapon, proficiency: 30);
        var defender = MakeCombatant(defense: 20);
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
        var attacker = MakeCombatant();
        var defender = MakeCombatant(defense: 999);
        var calculator = new HitCalculator(AlwaysMiss);

        // Act
        var didHit = calculator.DidHit(attacker, MakeAttack(DamageType.Fire), defender);

        // Assert
        Assert.True(didHit);
    }

    [Fact]
    public void DidHit_RespectsTheClampedChance_ForPhysicalAbilities()
    {
        // Arrange
        var attacker = MakeCombatant();
        var defender = MakeCombatant();
        var alwaysHitCalculator = new HitCalculator(AlwaysHit);
        var alwaysMissCalculator = new HitCalculator(AlwaysMiss);

        // Act & Assert
        Assert.True(alwaysHitCalculator.DidHit(attacker, MakeAttack(), defender));
        Assert.False(alwaysMissCalculator.DidHit(attacker, MakeAttack(), defender));
    }

    [Fact]
    public void DidBlock_NeverBlocks_ForNonPhysicalAbilities()
    {
        // Arrange — a shield that would otherwise always block
        var shield = new ShieldItem
        {
            WorldId = _worldId,
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test shield.",
            BlockChance = 1.0f,
        };
        var creature = Builders.MakeCreature(_worldId);
        var defender = Combatant.FromCreature(creature, [], BasicAttack, true, [shield], []);
        var calculator = new HitCalculator(Settings);

        // Act
        var didBlock = calculator.DidBlock(MakeAttack(DamageType.Fire), defender);

        // Assert
        Assert.False(didBlock);
    }

    [Fact]
    public void DidBlock_RollsAgainstBlockChance_ForPhysicalAbilities()
    {
        // Arrange
        var shieldWorldId = _worldId;
        var blockingShield = new ShieldItem
        {
            WorldId = shieldWorldId,
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test shield.",
            BlockChance = 1.0f,
        };
        var creatureWithShield = Builders.MakeCreature(_worldId);
        var blockingDefender = Combatant.FromCreature(
            creatureWithShield,
            [],
            BasicAttack,
            true,
            [blockingShield],
            []
        );
        var unshieldedDefender = MakeCombatant();
        var calculator = new HitCalculator(Settings);

        // Act & Assert
        Assert.True(calculator.DidBlock(MakeAttack(), blockingDefender));
        Assert.False(calculator.DidBlock(MakeAttack(), unshieldedDefender));
    }
}
