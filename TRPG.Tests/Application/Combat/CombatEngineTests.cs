using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatEngineTests
{
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;
    private static readonly BuffAbility BlockStance = AbilityDefinitions.Create().BlockStance;
    private static readonly StatFormulas Formulas = Builders.MakeStatFormulas();
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysHit =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 1.0f, MaxHitChance = 1.0f }
        );

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysMiss =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { MinHitChance = 0.0f, MaxHitChance = 0.0f }
        );

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

    private Combatant MakeCombatant(
        string name,
        bool isPlayer = false,
        int endurance = 10,
        int dexterity = 10,
        int strength = 0,
        int stamina = 10,
        int defense = 0,
        IReadOnlyList<Ability>? abilities = null,
        WeaponItem? weapon = null,
        IReadOnlyList<UsableItem>? usableItems = null
    )
    {
        var creature = Builders.MakeCreature(_worldId, name: name);
        creature.BaseAttributes.Endurance = endurance;
        creature.BaseAttributes.Dexterity = dexterity;
        creature.BaseAttributes.Strength = strength;
        creature.BaseAttributes.Stamina = stamina;
        creature.BaseAttributes.Defense = defense;
        creature.BaseAttributes.MaximumHp = Formulas.CalculateMaximumHp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumAp = Formulas.CalculateMaximumAp(creature.BaseAttributes);
        creature.BaseAttributes.MaximumMp = Formulas.CalculateMaximumMp(creature.BaseAttributes);
        creature.CurrentHp = creature.BaseAttributes.MaximumHp;
        creature.CurrentAp = creature.BaseAttributes.MaximumAp;
        creature.CurrentMp = creature.BaseAttributes.MaximumMp;
        var inventory = weapon != null ? new Item[] { weapon } : [];
        return Combatant.FromCreature(
            creature,
            abilities ?? [],
            BasicAttack,
            BlockStance,
            isPlayer,
            inventory,
            new Dictionary<WeaponType, int>(),
            usableItems ?? []
        );
    }

    [Fact]
    public void ResolvePlayerAction_ResolvesFullRound_PlayerAndEnemies()
    {
        // Arrange
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            strength: 100,
            abilities: [MakeAttack("Smite", damage: 100)]
        );
        var monster = MakeCombatant("Wraith", endurance: 1, abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true, endurance: 1, dexterity: 1);
        var monster = MakeCombatant(
            "Wraith",
            dexterity: 50,
            strength: 100,
            abilities: [MakeAttack("Crush", damage: 100)]
        );
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant(
            "Wraith",
            stamina: 1,
            abilities: [MakeAttack("Devour", cost: 99)]
        );
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [MakeAttack("Bash", status: stun)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant(
            "Wraith",
            abilities: [MakeAttack(damageType: DamageType.Physical)]
        );
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant(
            "Wraith",
            abilities: [MakeAttack("Fireball", damageType: DamageType.Fire)]
        );
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant(
            "Wraith",
            abilities: [MakeAttack("Fireball", damageType: DamageType.Fire)]
        );
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant(
            "Wraith",
            abilities: [MakeAttack(damageType: DamageType.Physical)]
        );
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [MakeAttack("Cleave", targetType: AttackTargetType.Aoe)]
        );
        var first = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        var second = MakeCombatant("Husk", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, first, second];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbility("Cleave", "Wraith"));

        // Assert — one Hit entry per target
        var cleaveHits = state.Events.OfType<Hit>().Where(h => h.AbilityName == "Cleave").ToArray();
        Assert.Equal(2, cleaveHits.Length);
        Assert.Equal(
            new[] { "Husk", "Wraith" },
            cleaveHits.Select(h => h.TargetName).OrderBy(n => n).ToArray()
        );
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20, weapon: weapon);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            stamina: 1,
            abilities: [MakeAttack("Devour", cost: 99)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        monster.CurrentHp = 0;
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            abilities: [MakeSupport("Battle Stance", targetType: TargetType.Self)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            abilities: [MakeAttack("Arcane Blast", mpCost: 99)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [MakeAttack("Smite", cooldown: 2)]
        );
        var monster = MakeCombatant("Wraith", endurance: 100, abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [MakeAttack("Ignite", damage: 1, damageType: DamageType.Fire, dot: burn)]
        );
        var monster = MakeCombatant("Wraith", endurance: 100, abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [buffAbility]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            strength: 0,
            abilities: [buffAbility]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act — cast the same buff twice in a row
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [battleStance, ironWill]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act — cast two different-named buffs
        Resolve(engine, combatants, new UseAbility("Battle Stance", "Hero"));
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20, abilities: [ignite]);
        var monster = MakeCombatant("Wraith", endurance: 100, abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act — reapply the same DoT-inflicting ability twice
        Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [ignite, venom]
        );
        var monster = MakeCombatant("Wraith", endurance: 100, abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act — apply two different-named DoTs
        Resolve(engine, combatants, new UseAbility("Ignite", "Wraith"));
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20, abilities: [regen]);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act — reapply the same HoT ability twice
        Resolve(engine, combatants, new UseAbility("Regen", "Hero"));
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
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            dexterity: 20,
            abilities: [regen, rejuvenate]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act — apply two different-named HoTs
        Resolve(engine, combatants, new UseAbility("Regen", "Hero"));
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20, defense: 10);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
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
        var player = MakeCombatant("Hero", isPlayer: true, dexterity: 20);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        Resolve(engine, combatants, new UseAbility("Block", "Hero"));

        // Assert
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(player.MaximumAp - BlockStance.ApCost, playerState.CurrentAp);
    }
}
