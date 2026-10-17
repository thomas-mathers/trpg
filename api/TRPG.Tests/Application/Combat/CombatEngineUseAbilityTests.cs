using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Effects;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatEngineUseAbilityTests
{
    private static readonly CombatOptions Options = new()
    {
        MinHitChance = 1f,
        MaxHitChance = 1f,
        CritChancePerDexterityPoint = 0f,
    };

    private static readonly Point Origin = new(0, 0);
    private static readonly Point InFront = new(0, -2);
    private static readonly Point Behind = new(0, 2);
    private static readonly Point Far = new(0, -30);

    private static readonly AttackAbility Slash = new() { Name = "Slash" };

    private readonly CombatEngine _engine = MakeEngine();

    private static CombatEngine MakeEngine()
    {
        var snapshot = new TestOptionsSnapshot<CombatOptions>(Options);
        var hitCalculator = new HitCalculator(snapshot);
        var damageCalculator = new DamageCalculator(snapshot);
        var timeScale = EffectTimeScale.Unscaled;

        return new CombatEngine(
            snapshot,
            hitCalculator,
            damageCalculator,
            new EnemyCombatActionResolver(snapshot, damageCalculator, hitCalculator),
            new EffectAdvancer(snapshot, timeScale),
            timeScale
        );
    }

    private static (Combatant Player, Combatant Monster) MakeFight(
        Point monsterPosition,
        params Ability[] playerAbilities
    )
    {
        var player = Builders
            .NewCombatant()
            .WithName("Hero")
            .WithCombatOptions(Options)
            .WithAbilities(playerAbilities)
            .AsPlayer()
            .Build();
        var monster = Builders.NewCombatant().WithName("Wraith").WithCombatOptions(Options).Build();
        player.Position = Origin;
        player.Heading = 0;
        monster.Position = monsterPosition;

        return (player, monster);
    }

    private AbilityUseResult Use(
        Combatant actor,
        Combatant target,
        Combatant other,
        string abilityName,
        GameInstant at
    ) => _engine.UseAbility([actor, other], actor.CreatureId, target.CreatureId, abilityName, at);

    [Fact]
    public void UseAbility_HitsTheTarget_WhenInRangeAndFacing()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront);

        // Act
        var result = Use(player, monster, monster, "Strike", TestTime.Start);

        // Assert
        Assert.Single(result.State!.Events.OfType<Hit>());
    }

    [Fact]
    public void UseAbility_Rejects_WhenTheTargetIsBeyondMaxRange()
    {
        // Arrange
        var (player, monster) = MakeFight(Far);

        // Act
        var result = Use(player, monster, monster, "Strike", TestTime.Start);

        // Assert
        Assert.Equal(AbilityUseRejection.OutOfRange, result.Rejection);
    }

    [Fact]
    public void UseAbility_Rejects_WhenTheTargetIsInsideMinRange()
    {
        // Arrange
        var snipe = new AttackAbility { Name = "Snipe", Range = new AbilityRange(5, 20) };
        var (player, monster) = MakeFight(new Point(0, -1), snipe);

        // Act
        var result = Use(player, monster, monster, "Snipe", TestTime.Start);

        // Assert
        Assert.Equal(AbilityUseRejection.TooClose, result.Rejection);
    }

    [Fact]
    public void UseAbility_Rejects_WhenTheTargetIsBehindTheActor()
    {
        // Arrange
        var (player, monster) = MakeFight(Behind);

        // Act
        var result = Use(player, monster, monster, "Strike", TestTime.Start);

        // Assert
        Assert.Equal(AbilityUseRejection.NotFacing, result.Rejection);
    }

    [Fact]
    public void UseAbility_AcceptsAMaxRangeOvershoot_WithinTheStalenessTolerance()
    {
        // Arrange
        var overshoot = new Point(0, -2.0 - (AbilityUseValidator.RangeTolerance / 2));
        var (player, monster) = MakeFight(overshoot);

        // Act
        var result = Use(player, monster, monster, "Strike", TestTime.Start);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_Rejects_WhileTheGlobalCooldownIsRunning()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront, Slash);
        Use(player, monster, monster, "Slash", TestTime.Start);

        // Act
        var result = Use(
            player,
            monster,
            monster,
            "Slash",
            TestTime.Start + TimeSpan.FromSeconds(1)
        );

        // Assert
        Assert.Equal(AbilityUseRejection.GlobalCooldown, result.Rejection);
    }

    [Fact]
    public void UseAbility_Succeeds_OnceTheGlobalCooldownHasElapsed()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront, Slash);
        Use(player, monster, monster, "Slash", TestTime.Start);

        // Act
        var result = Use(
            player,
            monster,
            monster,
            "Slash",
            TestTime.Start + CombatTiming.GlobalCooldown
        );

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_IgnoresTheGlobalCooldown_ForAbilitiesThatDoNotTriggerIt()
    {
        // Arrange
        var kick = new AttackAbility { Name = "Kick", TriggersGlobalCooldown = false };
        var (player, monster) = MakeFight(InFront, Slash, kick);
        Use(player, monster, monster, "Slash", TestTime.Start);

        // Act
        var result = Use(player, monster, monster, "Kick", TestTime.Start);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_DoesNotStartTheGlobalCooldown_WhenStriking()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront, Slash);
        Use(player, monster, monster, "Strike", TestTime.Start);

        // Act
        var result = Use(player, monster, monster, "Slash", TestTime.Start);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_Strikes_DuringTheGlobalCooldown()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront, Slash);
        Use(player, monster, monster, "Slash", TestTime.Start);

        // Act
        var result = Use(player, monster, monster, "Strike", TestTime.Start);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_Rejects_WhenStrikingBeforeTheSwingTimerIsReady()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront);
        Use(player, monster, monster, "Strike", TestTime.Start);

        // Act
        var result = Use(
            player,
            monster,
            monster,
            "Strike",
            TestTime.Start + TimeSpan.FromSeconds(1)
        );

        // Assert
        Assert.Equal(AbilityUseRejection.NotReady, result.Rejection);
    }

    [Fact]
    public void UseAbility_Strikes_OnceTheSwingIntervalHasElapsed()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront);
        Use(player, monster, monster, "Strike", TestTime.Start);

        // Act
        var result = Use(
            player,
            monster,
            monster,
            "Strike",
            TestTime.Start + TimeSpan.FromSeconds(Options.AutoAttackIntervalSeconds)
        );

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UseAbility_ShortensTheSwingInterval_WithIncreasedAttackSpeed()
    {
        // Arrange
        var quickBlade = Builders.MakeWeapon(
            modifiers:
            [
                new CombatSpeedModifier
                {
                    SpeedType = CombatSpeedType.IncreasedAttackSpeed,
                    Amount = 100,
                },
            ]
        );
        var player = Builders
            .NewCombatant()
            .WithCombatOptions(Options)
            .WithItem(quickBlade)
            .AsPlayer()
            .Build();
        var monster = Builders.NewCombatant().WithCombatOptions(Options).Build();
        monster.Position = InFront;
        Use(player, monster, monster, "Strike", TestTime.Start);

        // Act
        var result = Use(
            player,
            monster,
            monster,
            "Strike",
            TestTime.Start + TimeSpan.FromSeconds(Options.AutoAttackIntervalSeconds / 2)
        );

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AutoAttack_HitsTheTarget_WhenTheSwingTimerIsReady()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront);

        // Act
        var result = _engine.AutoAttack(
            [player, monster],
            player.CreatureId,
            monster.CreatureId,
            TestTime.Start
        );

        // Assert
        Assert.Single(result.State!.Events.OfType<Hit>());
    }

    [Fact]
    public void AutoAttack_Rejects_WhenTheTargetIsOutOfReach()
    {
        // Arrange
        var (player, monster) = MakeFight(Far);

        // Act
        var result = _engine.AutoAttack(
            [player, monster],
            player.CreatureId,
            monster.CreatureId,
            TestTime.Start
        );

        // Assert
        Assert.Equal(AbilityUseRejection.OutOfRange, result.Rejection);
    }

    [Fact]
    public void UseAbility_RejectsWithoutSpendingResources_WhenTheActorIsStunned()
    {
        // Arrange
        var bash = new AttackAbility { Name = "Bash", ApCost = 1 };
        var (player, monster) = MakeFight(InFront, bash);
        var apBefore = player.CurrentAp;
        player.ActiveConditions[ConditionType.Stunned] = TestTime.Start + TimeSpan.FromMinutes(1);

        // Act
        var result = Use(player, monster, monster, "Bash", TestTime.Start);

        // Assert
        Assert.Equal(AbilityUseRejection.Incapacitated, result.Rejection);
        Assert.Equal(apBefore, player.CurrentAp);
    }

    [Fact]
    public void UseAbility_Rejects_WhenAnAttackTargetsTheActor()
    {
        // Arrange
        var (player, monster) = MakeFight(InFront);

        // Act
        var result = Use(player, player, monster, "Strike", TestTime.Start);

        // Assert
        Assert.Equal(AbilityUseRejection.InvalidTarget, result.Rejection);
    }

    [Fact]
    public void UseAbility_AppliesASupportAbilityToTheActor_RegardlessOfRangeAndFacing()
    {
        // Arrange
        var (player, monster) = MakeFight(Far, AbilityCatalog.Block);

        // Act
        var result = Use(player, player, monster, AbilityCatalog.Block.Name, TestTime.Start);

        // Assert
        Assert.True(result.Succeeded);
    }
}
