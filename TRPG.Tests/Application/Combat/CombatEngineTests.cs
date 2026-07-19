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

    private Combatant MakeCombatant(
        string name,
        bool isPlayer = false,
        int endurance = 10,
        int dexterity = 10,
        int strength = 0,
        int stamina = 10,
        IReadOnlyList<Ability>? abilities = null,
        WeaponItem? weapon = null
    )
    {
        var creature = Builders.MakeCreature(_worldId, name: name);
        creature.Attributes.Endurance = endurance;
        creature.Attributes.Dexterity = dexterity;
        creature.Attributes.Strength = strength;
        creature.Attributes.Stamina = stamina;
        creature.Attributes.Defense = 0;
        creature.Attributes.MaximumHp = StatFormulas.CalculateMaximumHp(creature.Attributes);
        creature.Attributes.MaximumAp = StatFormulas.CalculateMaximumAp(creature.Attributes);
        creature.Attributes.MaximumMp = StatFormulas.CalculateMaximumMp(creature.Attributes);
        creature.CurrentHp = creature.Attributes.MaximumHp;
        creature.CurrentAp = creature.Attributes.MaximumAp;
        creature.CurrentMp = creature.Attributes.MaximumMp;
        var inventory = weapon != null ? new Item[] { weapon } : [];
        return Combatant.FromCreature(
            creature,
            abilities ?? [],
            BasicAttack,
            isPlayer,
            inventory,
            new Dictionary<WeaponType, int>()
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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Smite", "Wraith");

        // Assert — the dead enemy never got its turn, and the spoils are reported
        Assert.Equal(CombatOutcome.Victory, state.Outcome);
        var hit = Assert.IsType<Hit>(Assert.Single(state.Events));
        Assert.True(hit.Killed);
        Assert.False(state.Combatants.Single(c => !c.IsPlayer).IsAlive);
        Assert.NotNull(state.XpGained);
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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Bash", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Cleave", "Wraith");

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
        var state = engine.ProcessRound(combatants, "Strike", "Wraith");

        // Assert — the player's swing is tracked; the monster's (unarmed) attack is not
        var swing = Assert.Single(state.WeaponSwingCounts);
        Assert.Equal(WeaponType.Sword, swing.Key);
        Assert.Equal(1, swing.Value);
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenAbilityIsUnknownOrUnaffordable()
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
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert — neither consumed the round
        Assert.Throws<ArgumentException>(() =>
            engine.ProcessRound(combatants, "Fireball", "Wraith")
        );
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Devour", "Wraith")
        );
        Assert.Equal(player.MaximumHp, player.CurrentHp);
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenTargetIsUnknown()
    {
        // Arrange
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => engine.ProcessRound(combatants, "Strike", "Ghost"));
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenTargetIsAlreadyDead()
    {
        // Arrange
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        monster.CurrentHp = 0;
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Strike", "Wraith")
        );
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenSelfOnlyAbilityTargetsSomeoneElse()
    {
        // Arrange
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            abilities: [MakeSupport("Battle Stance", targetType: TargetType.Self)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Battle Stance", "Wraith")
        );
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenAttackAbilityTargetsThePlayer()
    {
        // Arrange
        var player = MakeCombatant("Hero", isPlayer: true);
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Strike", "Hero")
        );
    }

    [Fact]
    public void ResolvePlayerAction_Throws_WhenPlayerCannotAffordMpCost()
    {
        // Arrange
        var player = MakeCombatant(
            "Hero",
            isPlayer: true,
            abilities: [MakeAttack("Arcane Blast", mpCost: 99)]
        );
        var monster = MakeCombatant("Wraith", abilities: [MakeAttack()]);
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Arcane Blast", "Wraith")
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
        engine.ProcessRound(combatants, "Smite", "Wraith");

        // Assert — still cooling down next round, available again after two full rounds
        Assert.Throws<InvalidOperationException>(() =>
            engine.ProcessRound(combatants, "Smite", "Wraith")
        );
        engine.ProcessRound(combatants, "Strike", "Wraith");
        engine.ProcessRound(combatants, "Strike", "Wraith");
        var state = engine.ProcessRound(combatants, "Smite", "Wraith");
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
        var state = engine.ProcessRound(combatants, "Ignite", "Wraith");

        // Assert — the burn ticked at round end as its own Turn entry, identified by ability and damage type
        var tick = Assert.Single(state.Events.OfType<DamageTicked>());
        Assert.Equal("Wraith", tick.CreatureName);
        Assert.Equal("Ignite", tick.AbilityName);
        Assert.Equal(DamageType.Fire, tick.DamageType);
    }
}
