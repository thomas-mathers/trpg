using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Contracts.Combat.Requests;
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
        PlayerCombatAction action
    )
    {
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(action);
        Assert.NotNull(resolution.Result);
        return engine.ProcessRound(combatants, resolution.Result);
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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Bash"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        var state = Resolve(engine, combatants, new UseAbilityAction(first.CreatureId, "Cleave"));

        // Assert — one Hit entry per target
        var cleaveHits = state.Events.OfType<Hit>().Where(h => h.AbilityName == "Cleave").ToArray();
        Assert.Equal(2, cleaveHits.Length);
        Assert.Equal(CleaveTargets, cleaveHits.Select(h => h.TargetName).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void ResolvePlayerAction_TracksWeaponSwings_ForThePlayersPhysicalAttacks()
    {
        // Arrange
        var weapon = new Weapon
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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — the player's swing is tracked; the monster's (unarmed) attack is not
        var swing = Assert.Single(state.WeaponSwingCounts);
        Assert.Equal(WeaponType.Sword, swing.Key);
        Assert.Equal(1, swing.Value);
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenAbilityIsUnknownOrUnaffordable()
    {
        // Arrange
        var devour = MakeAttack("Devour", cost: 99);
        var player = MakeCombatant("Hero").AsPlayer().WithStamina(1).WithAbilities(devour).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act & Assert — neither consumed the round
        var unknownAbility = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Fireball")
        );
        Assert.Equal("Ability Fireball not found", unknownAbility.ErrorMessage);

        var unaffordableAbility = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Devour")
        );
        Assert.Equal(
            $"Ability Devour costs {devour.ApCost} AP but {player.Name} only has {player.CurrentAp}",
            unaffordableAbility.ErrorMessage
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
        var unknownTargetId = Guid.NewGuid();

        // Act
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(unknownTargetId, "Strike")
        );

        // Assert
        Assert.Equal($"Target {unknownTargetId} not found", resolution.ErrorMessage);
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenTargetIsAlreadyDead()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).WithCurrentHp(0).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Strike")
        );

        // Assert
        Assert.Equal($"Target {monster.CreatureId} is already dead", resolution.ErrorMessage);
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

        // Act
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Battle Stance")
        );

        // Assert
        Assert.Equal(
            $"Ability Battle Stance can only be cast on {player.Name}",
            resolution.ErrorMessage
        );
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenAttackAbilityTargetsThePlayer()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(player.CreatureId, "Strike")
        );

        // Assert
        Assert.Equal($"Ability Strike cannot target {player.Name}", resolution.ErrorMessage);
    }

    [Fact]
    public void ResolvePlayerAction_IsRejected_WhenPlayerCannotAffordMpCost()
    {
        // Arrange
        var arcaneBlast = MakeAttack("Arcane Blast", mpCost: 99);
        var player = MakeCombatant("Hero").AsPlayer().WithAbilities(arcaneBlast).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Arcane Blast")
        );

        // Assert
        Assert.Equal(
            $"Ability Arcane Blast costs {arcaneBlast.MpCost} MP but {player.Name} only has {player.CurrentMp}",
            resolution.ErrorMessage
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
        var smite = MakeAttack("Smite", cooldown: 2);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(smite)
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithEndurance(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));

        // Assert — still cooling down next round, available again after two full rounds
        var stillOnCooldown = new PlayerCombatActionResolver(combatants).Resolve(
            new UseAbilityAction(monster.CreatureId, "Smite")
        );
        Assert.Equal(
            $"Ability 'Smite' is on cooldown for {smite.Cooldown} more round(s).",
            stillOnCooldown.ErrorMessage
        );
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));
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
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Ignite"));

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
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Battle Stance"));
        var afterCast = combatants.Single(c => c.IsPlayer);
        Assert.Equal(3, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));
        Assert.Equal(2, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));
        Assert.Equal(1, Assert.Single(afterCast.ActiveBuffs).RemainingTurns);

        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

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
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Battle Stance"));

        // Act — cast the same buff again
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Battle Stance"));

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
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Battle Stance"));

        // Act — cast a second, different-named buff
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Iron Will"));

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
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Ignite"));

        // Act — reapply the same DoT-inflicting ability
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Ignite"));

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
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Ignite"));

        // Act — apply a second, different-named DoT
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Venom"));

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
        var regen = MakeRegen();
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(regen)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Regen"));

        // Act — reapply the same HoT ability
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Regen"));

        // Assert — refreshed, not stacked into a second entry
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Single(playerState.ActiveHots);
    }

    [Fact]
    public void ApplyHealOverTime_AllowsDifferentNamedHots_ToCoexist()
    {
        // Arrange
        var regen = MakeRegen();
        var rejuvenate = MakeRegen("Rejuvenate", amountPerTurn: 3);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(regen, rejuvenate)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Regen"));

        // Act — apply a second, different-named HoT
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Rejuvenate"));

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
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Block"));

        // Assert — Defense is doubled via the buff, not a bespoke damage-mitigation path
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(20, playerState.CalculateEffectiveAttribute(AttributeName.Defense));
        Assert.Contains(
            playerState.ActiveBuffs,
            b => b is { AbilityName: "Block", Attribute: AttributeName.Defense }
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
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Block"));

        // Assert
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumAp - BlockStance.ApCost, playerState.CurrentAp);
    }

    [Fact]
    public void ProcessItem_RestoresHp_ClampedToMaximum()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 999,
        };
        var player = MakeCombatant("Hero").AsPlayer().WithItem(potion).WithCurrentHp(1).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItemAction("Health Potion"));

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
        var potion = new Consumable
        {
            Name = "Ap Tonic",
            Resource = ResourceType.Ap,
            RestoreAmount = 999,
        };
        var player = MakeCombatant("Hero").AsPlayer().WithItem(potion).WithCurrentAp(1).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItemAction("Ap Tonic"));

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
        var potion = new Consumable
        {
            Name = "Mp Tonic",
            Resource = ResourceType.Mp,
            RestoreAmount = 999,
        };
        var player = MakeCombatant("Hero").AsPlayer().WithItem(potion).WithCurrentMp(1).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItemAction("Mp Tonic"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumMp, playerState.CurrentMp);
        var consumed = Assert.IsType<ConsumedPotion>(
            Assert.Single(state.Events, e => e is ConsumedPotion)
        );
        Assert.Equal(ResourceType.Mp, consumed.Resource);
    }

    [Fact]
    public void ProcessItem_RecordsItemUsage_InItemsUsedCounts()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 20,
        };
        var player = MakeCombatant("Hero").AsPlayer().WithItem(potion).WithCurrentHp(1).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseItemAction("Health Potion"));

        // Assert — this is what ResolveCombatRoundCommand reads to deplete inventory
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        var (itemId, count) = Assert.Single(playerState.ItemsUsedCounts);
        Assert.Equal(potion.Id, itemId);
        Assert.Equal(1, count);
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
        var state = Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Cure"));

        // Assert
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
        var healed = Assert.IsType<Healed>(Assert.Single(state.Events, e => e is Healed));
        Assert.Equal("Hero", healed.TargetName);
    }

    [Fact]
    public void ResolveEnemyAction_DrinksHealthPotion_WhenLowOnHpAndNoHealAbility()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentHp(1).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — drank the potion instead of attacking
        var consumed = Assert.IsType<ConsumedPotion>(state.Events[1]);
        Assert.Equal(ResourceType.Hp, consumed.Resource);
    }

    [Fact]
    public void ResolveEnemyAction_UsesHealAbility_OverHealthPotion_WhenBothAreAvailable()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(MakeInstantHeal())
            .WithItem(potion)
            .WithCurrentHp(1)
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — the ability is preferred; the potion is left untouched
        var healed = Assert.IsType<Healed>(state.Events[1]);
        Assert.Equal("Wraith", healed.TargetName);
    }

    [Fact]
    public void ResolveEnemyAction_DrinksApPotion_WhenHpIsFineButApIsLow()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Ap Tonic",
            Resource = ResourceType.Ap,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentAp(1).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert
        var consumed = Assert.IsType<ConsumedPotion>(state.Events[1]);
        Assert.Equal(ResourceType.Ap, consumed.Resource);
    }

    [Fact]
    public void ResolveEnemyAction_DrinksMpPotion_WhenHpIsFineButMpIsLow()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Mp Tonic",
            Resource = ResourceType.Mp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentMp(1).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert
        var consumed = Assert.IsType<ConsumedPotion>(state.Events[1]);
        Assert.Equal(ResourceType.Mp, consumed.Resource);
    }

    [Fact]
    public void ResolveEnemyAction_FallsBackToBlock_WhenLowOnHpWithNoHealOrPotionForIt()
    {
        // Arrange — an Ap potion is present but doesn't address the emergency, so it's ignored
        var apPotion = new Consumable
        {
            Name = "Ap Tonic",
            Resource = ResourceType.Ap,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(apPotion).WithCurrentHp(1).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — raises its guard as a last resort rather than drinking an unrelated potion
        var buffApplied = Assert.IsType<BuffApplied>(state.Events[1]);
        Assert.Equal("Block", buffApplied.AbilityName);
    }

    [Fact]
    public void ResolveEnemyAction_CastsOpeningBuff_WhenHealthyAndNotYetActive()
    {
        // Arrange — a long-running buff (Duration > 1) is cast proactively, unlike Block
        var battleStance = MakeSupport("Battle Stance");
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(battleStance).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert
        var buffApplied = Assert.IsType<BuffApplied>(state.Events[1]);
        Assert.Equal("Battle Stance", buffApplied.AbilityName);
    }
}
