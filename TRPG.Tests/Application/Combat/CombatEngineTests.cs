using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatEngineTests
{
    private static readonly BuffAbility BlockStance = AbilityDefinitions.Create().BlockStance;
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysHit =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 1.0f, MaxHitChance = 1.0f }
        );

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysMiss =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 0.0f, MaxHitChance = 0.0f }
        );

    private static readonly string[] CleaveTargets = ["Husk", "Wraith"];

    private static CombatEngine MakeEngine(IOptionsSnapshot<CombatOptions> optionsSnapshot)
    {
        var hitCalculator = new HitCalculator(optionsSnapshot);
        var damageCalculator = new DamageCalculator(optionsSnapshot);
        return new CombatEngine(optionsSnapshot, hitCalculator, damageCalculator);
    }

    private static CombatState Resolve(
        CombatEngine engine,
        IReadOnlyList<Combatant> combatants,
        PlayerRoundAction action
    )
    {
        var resolution = PlayerActionResolver.Resolve(combatants, action);
        var resolved = Assert.IsType<ActionResolved>(resolution);
        return engine.ProcessRound(combatants, resolved.Action);
    }

    private static AttackAbility MakeAttack(
        string name = "Claw",
        float damage = 5,
        int cost = 0,
        int mpCost = 0,
        int cooldown = 0,
        AttackTargetType targetType = AttackTargetType.Single,
        DamageType damageType = DamageType.Physical,
        DotEffect? dot = null,
        StatusEffect? status = null
    )
    {
        return new AttackAbility
        {
            Name = name,
            Description = "A test attack.",
            ApCost = cost,
            MpCost = mpCost,
            Cooldown = cooldown,
            TargetType = targetType,
            DamageType = damageType,
            DamageAmount = damage,
            DamageAmountType = AmountType.Flat,
            Dots = dot != null ? [dot] : [],
            Conditions = status != null ? [status] : [],
        };
    }

    private static BuffAbility MakeSupport(
        string name = "Buff",
        int cost = 0,
        int cooldown = 0,
        TargetType targetType = TargetType.Single
    )
    {
        return new BuffAbility
        {
            Name = name,
            Description = "A test support ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = targetType,
            Duration = 3,
        };
    }

    private static HealOverTimeAbility MakeRegen(
        string name = "Regen",
        int amountPerTurn = 5,
        int duration = 3,
        int cost = 0,
        int cooldown = 0
    )
    {
        return new HealOverTimeAbility
        {
            Name = name,
            Description = "A test heal-over-time ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = TargetType.Single,
            AmountPerTurn = amountPerTurn,
            Duration = duration,
        };
    }

    private static InstantHealAbility MakeInstantHeal(
        string name = "Cure",
        int amount = 20,
        int cost = 0,
        int cooldown = 0
    )
    {
        return new InstantHealAbility
        {
            Name = name,
            Description = "A test instant-heal ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = TargetType.Single,
            Amount = amount,
        };
    }

    private CombatantBuilder MakeCombatant(string name) =>
        Builders.NewCombatant().WithWorldId(_worldId).WithName(name);

    [Fact]
    public void ResolvePlayerAction_ResolvesFullRound_PlayerAndEnemies()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — player acted (faster) and the surviving enemy answered
        Assert.Equal(CombatOutcome.Ongoing, state.Outcome);
        Assert.Equal(2, state.Events.Count);
        Assert.Equal("Hero", Assert.IsType<Hit>(state.Events[0]).AttackerName);
        Assert.Equal("Wraith", Assert.IsType<Hit>(state.Events[1]).AttackerName);
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.True(playerState.CurrentHp < playerState.MaximumHp);
    }

    [Fact]
    public void ResolvePlayerAction_ReportsMisses_WhenTheHitRollFails()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert
        Assert.All(state.Events, t => Assert.IsType<Miss>(t));
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
    }

    [Fact]
    public void ResolvePlayerAction_EndsInVictory_WhenLastEnemyDies()
    {
        // Arrange — one fragile monster, one overwhelming attack
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithStrength(100)
            .WithAbilities(MakeAttack("Smite", damage: 100))
            .Build();
        var monster = MakeCombatant("Wraith").WithEndurance(1).WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Smite", "Wraith"));

        // Assert — the dead enemy never got its turn, and the spoils are reported
        Assert.Equal(CombatOutcome.Victory, state.Outcome);
        var hit = Assert.IsType<Hit>(Assert.Single(state.Events));
        Assert.True(hit.Killed);
        Assert.False(state.Combatants.Single(c => !c.IsPlayer).IsAlive);
        Assert.NotNull(state.GoldLooted);
    }

    [Fact]
    public void ResolvePlayerAction_EndsInDefeat_WhenPlayerDies()
    {
        // Arrange — monster outspeeds the player and hits like a landslide
        var player = MakeCombatant("Hero").AsPlayer().WithEndurance(1).WithDexterity(1).Build();
        var monster = MakeCombatant("Wraith")
            .WithDexterity(50)
            .WithStrength(100)
            .WithAbilities(MakeAttack("Crush", damage: 100))
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — the player never got to act
        Assert.Equal(CombatOutcome.Defeat, state.Outcome);
        var hit = Assert.IsType<Hit>(Assert.Single(state.Events));
        Assert.Equal("Wraith", hit.AttackerName);
        Assert.True(hit.Killed);
    }

    [Fact]
    public void ResolvePlayerAction_FallsBackToBasicAttack_WhenEnemyCannotAffordItsAbilities()
    {
        // Arrange — the enemy's only real attack costs more AP than it can ever have
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith")
            .WithStamina(1)
            .WithAbilities(MakeAttack("Devour", cost: 99))
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — it fell back to the always-affordable basic attack rather than skipping
        Assert.Equal(2, state.Events.Count);
        var monsterHit = Assert.IsType<Hit>(state.Events[1]);
        Assert.Equal("Strike", monsterHit.AbilityName);
    }

    [Fact]
    public void ResolvePlayerAction_SkipsStunnedCombatant_AndTicksTheCondition()
    {
        // Arrange
        var stun = new StatusEffect { Condition = ConditionType.Stunned, Duration = 2 };
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Bash", status: stun))
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Bash", "Wraith"));

        // Assert — the stunned enemy lost its turn and the condition is visible in its state
        var noAction = Assert.IsType<NoAction>(state.Events[1]);
        Assert.Equal(ConditionType.Stunned, noAction.Condition);
        var playerHit = Assert.IsType<Hit>(state.Events[0]);
        Assert.Equal(ConditionType.Stunned, Assert.Single(playerHit.AppliedConditions));
        var enemyState = state.Combatants.Single(c => !c.IsPlayer);
        Assert.True(enemyState.ActiveConditions.ContainsKey(ConditionType.Stunned));
    }

    [Fact]
    public void ResolvePlayerAction_SkipsFrozenCombatant_AndReportsNoAction()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        monster.ActiveConditions[ConditionType.Frozen] = 2;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — the frozen enemy lost its turn
        var noAction = Assert.IsType<NoAction>(state.Events[1]);
        Assert.Equal("Wraith", noAction.CreatureName);
        Assert.Equal(ConditionType.Frozen, noAction.Condition);
    }

    [Fact]
    public void ResolvePlayerAction_BlocksPhysicalAttack_WhenAttackerIsBlinded()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(MakeAttack(damageType: DamageType.Physical))
            .Build();
        monster.ActiveConditions[ConditionType.Blinded] = 1;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert
        var noAction = Assert.IsType<NoAction>(state.Events[1]);
        Assert.Equal(ConditionType.Blinded, noAction.Condition);
    }

    [Fact]
    public void ResolvePlayerAction_AllowsMagicalAttack_WhenAttackerIsBlinded()
    {
        // Arrange — blindness only blocks physical attacks
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(MakeAttack("Fireball", damageType: DamageType.Fire))
            .Build();
        monster.ActiveConditions[ConditionType.Blinded] = 1;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert
        Assert.IsType<Hit>(state.Events[1]);
    }

    [Fact]
    public void ResolvePlayerAction_BlocksMagicalAttack_WhenAttackerIsSilenced()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(MakeAttack("Fireball", damageType: DamageType.Fire))
            .Build();
        monster.ActiveConditions[ConditionType.Silenced] = 1;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert
        var noAction = Assert.IsType<NoAction>(state.Events[1]);
        Assert.Equal(ConditionType.Silenced, noAction.Condition);
    }

    [Fact]
    public void ResolvePlayerAction_AllowsPhysicalAttack_WhenAttackerIsSilenced()
    {
        // Arrange — silence only blocks magical attacks
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(MakeAttack(damageType: DamageType.Physical))
            .Build();
        monster.ActiveConditions[ConditionType.Silenced] = 1;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert
        Assert.IsType<Hit>(state.Events[1]);
    }

    [Fact]
    public void ResolvePlayerAction_HitsEveryEnemy_WhenAbilityIsAoe()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Cleave", targetType: AttackTargetType.Aoe))
            .Build();
        var first = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        var second = MakeCombatant("Husk").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, first, second];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Cleave", "Wraith"));

        // Assert — one Hit entry per target
        var cleaveHits = state.Events.OfType<Hit>().Where(h => h.AbilityName == "Cleave").ToArray();
        Assert.Equal(2, cleaveHits.Length);
        Assert.Equal(CleaveTargets, cleaveHits.Select(h => h.TargetName).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void ResolvePlayerAction_TracksWeaponSwings_ForThePlayersPhysicalAttacks()
    {
        // Arrange
        var weapon = new WeaponItem
        {
            WorldId = _worldId,
            Name = "Test Sword",
            Description = "A test weapon.",
            Type = WeaponType.Sword,
            MinDamage = 5,
            MaxDamage = 10,
        };
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).WithItem(weapon).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — the player's swing is tracked; the monster's (unarmed) attack is not
        var swing = Assert.Single(state.WeaponSwingCounts);
        Assert.Equal(WeaponType.Sword, swing.Key);
        Assert.Equal(1, swing.Value);
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenAbilityIsUnknownOrUnaffordable()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithStamina(1)
            .WithAbilities(MakeAttack("Devour", cost: 99))
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert — neither consumed the round
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Fireball", "Wraith"))
        );
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Devour", "Wraith"))
        );
        Assert.Equal(player.MaximumHp, player.CurrentHp);
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenTargetIsUnknown()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Strike", "Ghost"))
        );
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenTargetIsAlreadyDead()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).WithCurrentHp(0).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Strike", "Wraith"))
        );
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenSelfOnlyAbilityTargetsSomeoneElse()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithAbilities(MakeSupport("Battle Stance", targetType: TargetType.Self))
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Battle Stance", "Wraith"))
        );
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenAttackAbilityTargetsThePlayer()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Strike", "Hero"))
        );
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenPlayerCannotAffordMpCost()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithAbilities(MakeAttack("Arcane Blast", mpCost: 99))
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Arcane Blast", "Wraith"))
        );
    }

    [Fact]
    public void ResolveFlee_GivesEnemiesAPartingRound_ThenEndsTheFight()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = engine.ResolveFlee(combatants);

        // Assert
        Assert.Equal(CombatOutcome.Fled, state.Outcome);
        var partingHit = Assert.IsType<Hit>(Assert.Single(state.Events));
        Assert.Equal("Wraith", partingHit.AttackerName);
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.True(playerState.CurrentHp < playerState.MaximumHp);
    }

    [Fact]
    public void ResolvePlayerAction_RespectsCooldowns_AcrossRounds()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Smite", cooldown: 2))
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithEndurance(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        Resolve(engine, combatants, new UseAbility("Smite", "Wraith"));

        // Assert — still cooling down next round, available again after two full rounds
        Assert.IsType<ActionRejected>(
            PlayerActionResolver.Resolve(combatants, new UseAbility("Smite", "Wraith"))
        );
        Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));
        Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));
        var state = Resolve(engine, combatants, new UseAbility("Smite", "Wraith"));
        var smiteHit = Assert.IsType<Hit>(state.Events[0]);
        Assert.Equal("Smite", smiteHit.AbilityName);
    }

    [Fact]
    public void ResolvePlayerAction_TicksDamageOverTime_FromConditions()
    {
        // Arrange
        var burn = new DotEffect
        {
            Duration = 3,
            Amount = 2f,
            AmountType = AmountType.Flat,
        };
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Ignite", damage: 1, damageType: DamageType.Fire, dot: burn))
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithEndurance(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));

        // Assert — the burn ticked at round end as its own Turn entry, identified by ability and damage type
        var tick = Assert.Single(state.Events.OfType<DamageTicked>());
        Assert.Equal("Wraith", tick.CreatureName);
        Assert.Equal("Ignite", tick.AbilityName);
        Assert.Equal(DamageType.Fire, tick.DamageType);
    }

    [Fact]
    public void ProcessRound_TicksDownAndExpiresBuffs_OverSubsequentRounds()
    {
        // Arrange
        var buffAbility = MakeSupport(name: "Battle Stance");
        buffAbility.Modifiers.Add(
            new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = 5,
            }
        );
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(buffAbility)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act — cast the buff (duration 3), then let it tick down over subsequent rounds
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));
        var afterCast = combatants.Single(c => c.IsPlayer);
        Assert.Equal(3, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));
        Assert.Equal(2, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));
        Assert.Equal(1, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbility("Strike", "Wraith"));

        // Assert — expired after its duration elapsed
        Assert.Empty(afterCast.ActiveBuffs);
    }

    [Fact]
    public void ProcessRound_RefreshesExistingBuff_InsteadOfStacking_WhenSameAbilityReapplied()
    {
        // Arrange
        var buffAbility = MakeSupport(name: "Battle Stance");
        buffAbility.Modifiers.Add(
            new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = 5,
            }
        );
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithStrength(0)
            .WithAbilities(buffAbility)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));

        // Act — cast the same buff again
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));

        // Assert — refreshed in place, not stacked into a second entry
        var playerState = combatants.Single(c => c.IsPlayer);
        var buff = Assert.Single(playerState.ActiveBuffs);
        Assert.Equal(5, buff.Amount);
        Assert.Equal(5, playerState.CalculateEffectiveAttribute(AttributeName.Strength));
    }

    [Fact]
    public void ApplyBuff_AllowsDifferentNamedBuffs_ToCoexist()
    {
        // Arrange
        var battleStance = MakeSupport("Battle Stance");
        battleStance.Modifiers.Add(
            new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = 5,
            }
        );
        var ironWill = MakeSupport("Iron Will");
        ironWill.Modifiers.Add(
            new AttributeModifier
            {
                Attribute = AttributeName.Defense,
                AmountType = AmountType.Flat,
                Amount = 10,
            }
        );
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(battleStance, ironWill)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));

        // Act — cast a second, different-named buff
        Resolve(engine, combatants, new UseAbility("Iron Will", "Hero"));

        // Assert — both coexist, neither replaced the other
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(2, playerState.ActiveBuffs.Count);
    }

    [Fact]
    public void ApplyAttack_DoesNotStackDot_WhenSameAbilityReapplied()
    {
        // Arrange
        var burn = new DotEffect
        {
            Duration = 3,
            Amount = 2f,
            AmountType = AmountType.Flat,
        };
        var ignite = MakeAttack("Ignite", damage: 1, damageType: DamageType.Fire, dot: burn);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(ignite)
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithEndurance(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);
        Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));

        // Act — reapply the same DoT-inflicting ability
        Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));

        // Assert — refreshed, not stacked into a second entry
        var monsterState = combatants.Single(c => c.Name == "Wraith");
        Assert.Single(monsterState.ActiveDots);
    }

    [Fact]
    public void ApplyAttack_AllowsDifferentNamedDots_ToCoexist()
    {
        // Arrange
        var burn = new DotEffect
        {
            Duration = 3,
            Amount = 2f,
            AmountType = AmountType.Flat,
        };
        var poison = new DotEffect
        {
            Duration = 3,
            Amount = 1f,
            AmountType = AmountType.Flat,
        };
        var ignite = MakeAttack("Ignite", damage: 1, damageType: DamageType.Fire, dot: burn);
        var venom = MakeAttack("Venom", damage: 1, damageType: DamageType.Poison, dot: poison);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(ignite, venom)
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithEndurance(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);
        Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));

        // Act — apply a second, different-named DoT
        Resolve(engine, combatants, new UseAbility("Venom", "Wraith"));

        // Assert — both coexist, neither replaced the other
        var monsterState = combatants.Single(c => c.Name == "Wraith");
        Assert.Equal(2, monsterState.ActiveDots.Count);
        Assert.Contains(monsterState.ActiveDots, d => d.AbilityName == "Ignite");
        Assert.Contains(monsterState.ActiveDots, d => d.AbilityName == "Venom");
    }

    [Fact]
    public void ApplyHealOverTime_DoesNotStackHot_WhenSameAbilityReapplied()
    {
        // Arrange
        var regen = MakeRegen("Regen");
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(regen)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbility("Regen", "Hero"));

        // Act — reapply the same HoT ability
        Resolve(engine, combatants, new UseAbility("Regen", "Hero"));

        // Assert — refreshed, not stacked into a second entry
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Single(playerState.ActiveHots);
    }

    [Fact]
    public void ApplyHealOverTime_AllowsDifferentNamedHots_ToCoexist()
    {
        // Arrange
        var regen = MakeRegen("Regen");
        var rejuvenate = MakeRegen("Rejuvenate", amountPerTurn: 3);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(regen, rejuvenate)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbility("Regen", "Hero"));

        // Act — apply a second, different-named HoT
        Resolve(engine, combatants, new UseAbility("Rejuvenate", "Hero"));

        // Assert — both coexist, neither replaced the other
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(2, playerState.ActiveHots.Count);
    }

    [Fact]
    public void ProcessRound_Block_AppliesDefenseBuff_AlwaysAvailableLikeStrike()
    {
        // Arrange — "Block" is never passed via `abilities`, only ever added by MakeCombatant
        // itself (mirroring Strike), so resolving it here proves it's always available.
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).WithDefense(10).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        Resolve(engine, combatants, new UseAbility("Block", "Hero"));

        // Assert — Defense is doubled via the buff, not a bespoke damage-mitigation path
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(20, playerState.CalculateEffectiveAttribute(AttributeName.Defense));
        Assert.Contains(
            playerState.ActiveBuffs,
            b => b.AbilityName == "Block" && b.Attribute == AttributeName.Defense
        );
    }

    [Fact]
    public void ProcessRound_Block_DeductsItsApCost()
    {
        // Arrange — player starts at full AP, so this round's regen is a no-op and only the
        // ability's own cost should change CurrentAp
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        Resolve(engine, combatants, new UseAbility("Block", "Hero"));

        // Assert
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumAp - BlockStance.ApCost, playerState.CurrentAp);
    }

    [Fact]
    public void ProcessItem_RestoresHp_ClampedToMaximum()
    {
        // Arrange
        var potion = new UsableItem(Guid.NewGuid(), "Health Potion", ResourceType.Hp, 999);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithUsableItems(potion)
            .WithCurrentHp(1)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItem("Health Potion"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
        var consumed = Assert.IsType<ConsumedPotion>(
            Assert.Single(state.Events, e => e is ConsumedPotion)
        );
        Assert.Equal(ResourceType.Hp, consumed.Resource);
    }

    [Fact]
    public void ProcessItem_RestoresAp_ClampedToMaximum()
    {
        // Arrange
        var potion = new UsableItem(Guid.NewGuid(), "Ap Tonic", ResourceType.Ap, 999);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithUsableItems(potion)
            .WithCurrentAp(1)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItem("Ap Tonic"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumAp, playerState.CurrentAp);
        var consumed = Assert.IsType<ConsumedPotion>(
            Assert.Single(state.Events, e => e is ConsumedPotion)
        );
        Assert.Equal(ResourceType.Ap, consumed.Resource);
    }

    [Fact]
    public void ProcessItem_RestoresMp_ClampedToMaximum()
    {
        // Arrange
        var potion = new UsableItem(Guid.NewGuid(), "Mp Tonic", ResourceType.Mp, 999);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithUsableItems(potion)
            .WithCurrentMp(1)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItem("Mp Tonic"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumMp, playerState.CurrentMp);
        var consumed = Assert.IsType<ConsumedPotion>(
            Assert.Single(state.Events, e => e is ConsumedPotion)
        );
        Assert.Equal(ResourceType.Mp, consumed.Resource);
    }

    [Fact]
    public void ApplyInstantHeal_RestoresTargetHp_ClampedToMaximum()
    {
        // Arrange
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeInstantHeal(amount: 999))
            .WithCurrentHp(1)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Cure", "Hero"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
        var healed = Assert.IsType<Healed>(Assert.Single(state.Events, e => e is Healed));
        Assert.Equal("Hero", healed.TargetName);
    }
}
