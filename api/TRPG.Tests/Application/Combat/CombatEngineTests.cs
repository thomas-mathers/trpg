using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatEngineTests
{
    private static readonly SupportAbility BlockStance = AbilityCatalog.Block;
    private readonly Guid _worldId = Guid.NewGuid();

    // CritChancePerDexterityPoint is zeroed on both - a real random crit roll would otherwise
    // make these tests' exact HP/damage assertions flaky; crit behavior gets its own test instead.
    private static readonly IOptionsSnapshot<CombatOptions> AlwaysHit =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                MinHitChance = 1.0f,
                MaxHitChance = 1.0f,
                CritChancePerDexterityPoint = 0f,
            }
        );

    private static readonly IOptionsSnapshot<CombatOptions> AlwaysMiss =
        new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions
            {
                MinHitChance = 0.0f,
                MaxHitChance = 0.0f,
                CritChancePerDexterityPoint = 0f,
            }
        );

    // Matches AlwaysHit/AlwaysMiss on every field Combatant itself reads (only
    // CritChancePerDexterityPoint) so a combatant's own crit roll can't turn these tests flaky,
    // regardless of which of the two engine variants a given test pairs it with.
    private static readonly CombatOptions TestCombatOptions = new()
    {
        CritChancePerDexterityPoint = 0f,
    };

    private static readonly string[] CleaveTargets = ["Husk", "Wraith"];

    private static readonly IOptionsSnapshot<FleeOptions> DefaultFleeOptions =
        new TestOptionsSnapshot<FleeOptions>(new FleeOptions());

    private static readonly IOptionsSnapshot<FleeOptions> AlwaysEvades =
        new TestOptionsSnapshot<FleeOptions>(
            new FleeOptions { MinimumCatchChance = 0f, MaximumCatchChance = 0f }
        );

    private static readonly IOptionsSnapshot<FleeOptions> AlwaysCaught =
        new TestOptionsSnapshot<FleeOptions>(
            new FleeOptions { MinimumCatchChance = 1f, MaximumCatchChance = 1f }
        );

    private static CombatEngine MakeEngine(
        IOptionsSnapshot<CombatOptions> optionsSnapshot,
        IOptionsSnapshot<FleeOptions>? fleeOptionsSnapshot = null
    )
    {
        var hitCalculator = new HitCalculator(optionsSnapshot);
        var damageCalculator = new DamageCalculator(optionsSnapshot);
        var enemyCombatActionResolver = new EnemyCombatActionResolver(
            optionsSnapshot,
            damageCalculator,
            hitCalculator
        );
        return new CombatEngine(
            optionsSnapshot,
            fleeOptionsSnapshot ?? DefaultFleeOptions,
            hitCalculator,
            damageCalculator,
            enemyCombatActionResolver
        );
    }

    private static CombatState Resolve(
        CombatEngine engine,
        IReadOnlyList<Combatant> combatants,
        PlayerCombatAction action,
        bool isSurpriseRound = false
    )
    {
        var resolution = new PlayerCombatActionResolver(combatants).Resolve(action);
        Assert.NotNull(resolution.Result);
        return engine.ProcessRound(combatants, resolution.Result, isSurpriseRound);
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
        StatusEffect? status = null,
        AttributeEffect? debuff = null
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
            DamageAmountType =
                damageType == DamageType.Physical ? AmountType.Percent : AmountType.Flat,
            Dots = dot != null ? [dot] : [],
            Conditions = status != null ? [status] : [],
            Debuffs = debuff != null ? [debuff] : [],
        };
    }

    private static SupportAbility MakeRegen(
        string name = "Regen",
        int amountPerTurn = 5,
        int duration = 3,
        int cost = 0,
        int cooldown = 0
    )
    {
        return new SupportAbility
        {
            Name = name,
            Description = "A test heal-over-time ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = TargetType.Single,
            Hots = [new HotEffect { Amount = amountPerTurn, Duration = duration }],
        };
    }

    private CombatantBuilder MakeCombatant(string name) =>
        Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName(name)
            .WithCombatOptions(TestCombatOptions);

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
        var hits = state.Events.OfType<Hit>().ToArray();
        Assert.Equal(2, hits.Length);
        Assert.Equal("Hero", hits[0].AttackerName);
        Assert.Equal("Wraith", hits[1].AttackerName);
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.True(playerState.CurrentHp < playerState.MaximumHp);
    }

    [Fact]
    public void ProcessRound_OnlyPlayerActs_DuringASurpriseRound()
    {
        // Arrange — the monster's Dexterity would normally put it first in turn order
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(1).Build();
        var monster = MakeCombatant("Wraith")
            .WithDexterity(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(
            engine,
            combatants,
            new UseAbilityAction(monster.CreatureId, "Strike"),
            isSurpriseRound: true
        );

        // Assert — the enemy never gets a turn during a surprise round
        var attackerNames = state.Events.OfType<Hit>().Select(hit => hit.AttackerName).ToArray();
        Assert.DoesNotContain("Wraith", attackerNames);
        var monsterState = state.Combatants.Single(c => c.Name == "Wraith");
        Assert.True(monsterState.CurrentHp < monsterState.MaximumHp);
    }

    [Fact]
    public void ProcessRound_DoublesDamage_WhenTheAttackerIsMarkedAsASurpriseAttacker()
    {
        // Arrange — 5 base weapon damage x 2x sneak multiplier = 10
        var options = new CombatOptions
        {
            MinHitChance = 1f,
            MaxHitChance = 1f,
            CritChancePerDexterityPoint = 0f,
            SneakAttackDamageMultiplier = 2f,
        };
        var weapon = Builders.MakeWeapon(minDamage: 5, maxDamage: 5);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithItem(weapon)
            .WithCombatOptions(options)
            .WithIsSurpriseAttacker(true)
            .Build();
        var monster = MakeCombatant("Wraith").WithCombatOptions(options).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(new TestOptionsSnapshot<CombatOptions>(options));

        // Act
        var state = Resolve(
            engine,
            combatants,
            new UseAbilityAction(monster.CreatureId, "Strike"),
            isSurpriseRound: true
        );

        // Assert
        var hit = Assert.Single(state.Events.OfType<Hit>());
        Assert.Equal(10, hit.Damage);
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
        var misses = state.Events.OfType<Miss>().ToArray();
        Assert.Equal(2, misses.Length);
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
    }

    [Fact]
    public void ResolvePlayerAction_ResolvesABonusSwing_WhenAttackerWieldsAFastWeapon()
    {
        // Arrange — AttacksPerTurn 2 grants a bonus swing
        var dagger = Builders.MakeWeapon(attacksPerTurn: 2);
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).WithItem(dagger).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — one ability use with a fast weapon produces two Hit events for the attacker
        var playerHits = state.Events.OfType<Hit>().Count(h => h.AttackerName == "Hero");
        Assert.Equal(2, playerHits);
    }

    [Fact]
    public void ResolvePlayerAction_ResolvesOnlyOneSwing_WhenAttackerWieldsAStandardSpeedWeapon()
    {
        // Arrange — the default AttacksPerTurn (1) grants no bonus swing
        var sword = Builders.MakeWeapon();
        var player = MakeCombatant("Hero").AsPlayer().WithDexterity(20).WithItem(sword).Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert
        var playerHits = state.Events.OfType<Hit>().Count(h => h.AttackerName == "Hero");
        Assert.Equal(1, playerHits);
    }

    [Fact]
    public void ResolvePlayerAction_BonusSwingCarriesNoStatusAndLessDamage_ThanThePrimarySwing()
    {
        // Arrange — a high-percent ability with a status effect; only the first swing should
        // carry either, the bonus swing is a plain 100% weapon hit
        var dagger = Builders.MakeWeapon(minDamage: 5, maxDamage: 5, attacksPerTurn: 2);
        var stun = new StatusEffect { Condition = ConditionType.Stunned, Duration = 1 };
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Smite", damage: 500, status: stun))
            .WithItem(dagger)
            .Build();
        var monster = MakeCombatant("Wraith").Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));

        // Assert
        var playerHits = state.Events.OfType<Hit>().Where(h => h.AttackerName == "Hero").ToArray();
        Assert.Equal(2, playerHits.Length);
        Assert.NotEmpty(playerHits[0].AppliedConditions);
        Assert.Empty(playerHits[1].AppliedConditions);
        Assert.True(playerHits[0].Damage > playerHits[1].Damage);
    }

    [Fact]
    public void ResolvePlayerAction_ResolvesFourSwings_WhenDualWieldingFastWeapons()
    {
        // Arrange — two AttacksPerTurn=2 daggers combine into up to 4 attacks
        var mainDagger = Builders.MakeWeapon(type: WeaponType.Dagger, attacksPerTurn: 2);
        var offDagger = Builders.MakeWeapon(type: WeaponType.Dagger, attacksPerTurn: 2);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithItem(mainDagger)
            .WithItem(offDagger, slot: EquipmentSlot.LeftHand)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert
        var playerHits = state.Events.OfType<Hit>().Count(h => h.AttackerName == "Hero");
        Assert.Equal(4, playerHits);
    }

    [Fact]
    public void ResolvePlayerAction_OffHandBonusSwing_RollsOffHandWeaponsOwnDamage()
    {
        // Arrange — fixed, distinct damage ranges prove an off-hand swing uses OffHandWeapon's
        // own damage roll rather than the main-hand weapon's
        var mainDagger = Builders.MakeWeapon(minDamage: 5, maxDamage: 5, attacksPerTurn: 1);
        var offDagger = Builders.MakeWeapon(minDamage: 50, maxDamage: 50, attacksPerTurn: 1);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Smite", damage: 100))
            .WithItem(mainDagger)
            .WithItem(offDagger, slot: EquipmentSlot.LeftHand)
            .Build();
        var monster = MakeCombatant("Wraith").Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));

        // Assert
        var playerHits = state.Events.OfType<Hit>().Where(h => h.AttackerName == "Hero").ToArray();
        Assert.Equal(2, playerHits.Length);
        Assert.Equal(5, playerHits[0].Damage);
        Assert.Equal(50, playerHits[1].Damage);
    }

    [Fact]
    public void ResolvePlayerAction_EndsInVictory_WhenLastEnemyDies()
    {
        // Arrange — one fragile monster, one overwhelming attack
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithStrength(100)
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Smite", damage: 100))
            .Build();
        var monster = MakeCombatant("Wraith").WithEndurance(1).WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Smite"));

        // Assert — the dead enemy never got its turn, and the spoils are reported
        Assert.Equal(CombatOutcome.Victory, state.Outcome);
        var hit = Assert.Single(state.Events.OfType<Hit>());
        Assert.True(hit.Killed);
        Assert.False(state.Combatants.Single(c => !c.IsPlayer).IsAlive);
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
        var hit = Assert.Single(state.Events.OfType<Hit>());
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
        var monsterHit = Assert.Single(state.Events.OfType<Hit>(), h => h.AttackerName == "Wraith");
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
        var noAction = Assert.Single(state.Events.OfType<NoAction>());
        Assert.Equal(ConditionType.Stunned, noAction.Condition);
        var playerHit = Assert.Single(state.Events.OfType<Hit>(), h => h.AttackerName == "Hero");
        Assert.Equal(ConditionType.Stunned, Assert.Single(playerHit.AppliedConditions));
        var enemyState = state.Combatants.Single(c => !c.IsPlayer);
        Assert.True(enemyState.ActiveConditions.ContainsKey(ConditionType.Stunned));
    }

    [Fact]
    public void ResolvePlayerAction_AppliesTheAttacksDebuff_WhenItHits()
    {
        // Arrange
        var slow = new AttributeEffect
        {
            Attribute = AttributeName.Dexterity,
            AmountType = AmountType.Percent,
            Amount = -50,
            Duration = 2,
        };
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(MakeAttack("Hamstring", debuff: slow))
            .Build();
        var monster = MakeCombatant("Wraith")
            .WithDexterity(100)
            .WithAbilities(MakeAttack())
            .Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Hamstring"));

        // Assert — the debuff landed on the target and its effective Dexterity is halved
        var appliedDebuff = Assert.Single(monster.ActiveBuffs);
        Assert.Equal(AttributeName.Dexterity, appliedDebuff.Attribute);
        Assert.Equal(-50, appliedDebuff.Amount);
        Assert.Equal(2, appliedDebuff.RemainingTurns);
        Assert.Equal(50f, monster.Dexterity);
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
        var noAction = Assert.Single(state.Events.OfType<NoAction>());
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
        var noAction = Assert.Single(state.Events.OfType<NoAction>());
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
        Assert.Contains(state.Events.OfType<Hit>(), hit => hit.AttackerName == "Wraith");
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
        var noAction = Assert.Single(state.Events.OfType<NoAction>());
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
        Assert.Contains(state.Events.OfType<Hit>(), hit => hit.AttackerName == "Wraith");
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
            .WithAbilities(
                Builders.MakeBuffSupportAbility("Battle Stance", targetType: TargetType.Self)
            )
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
    public void ResolveFlee_EndsTheFightWithoutResolvingARound_WhenTheFleeAttemptSucceeds()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit, AlwaysEvades);

        // Act
        var state = engine.ResolveFlee(combatants);

        // Assert
        Assert.Equal(CombatOutcome.Fled, state.Outcome);
        Assert.Empty(state.Events);
        var playerState = state.Combatants.Single(c => c.IsPlayer);
        Assert.Equal(playerState.MaximumHp, playerState.CurrentHp);
    }

    [Fact]
    public void ResolveFlee_LeavesTheFightOngoing_WhenTheFleeAttemptIsCaught()
    {
        // Arrange
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit, AlwaysCaught);

        // Act
        var state = engine.ResolveFlee(combatants);

        // Assert
        Assert.Equal(CombatOutcome.Ongoing, state.Outcome);
        var fleeFailed = Assert.Single(state.Events);
        Assert.Equal(new FleeFailed(player.Name), fleeFailed);
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
        var smiteHit = Assert.Single(state.Events.OfType<Hit>(), h => h.AbilityName == "Smite");
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
        var buffAbility = Builders.MakeBuffSupportAbility(name: "Battle Stance");
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
    public void ProcessRound_TicksDownAndExpiresConditions_OverSubsequentRounds()
    {
        // Arrange
        var status = new StatusEffect { Condition = ConditionType.Blinded, Duration = 2 };
        var attackWithCondition = MakeAttack(name: "Sand Throw", status: status);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(attackWithCondition)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysHit);

        // Act — inflict the condition (duration 2). The player acts first (higher dexterity), so
        // the target's own tick later this same round already counts it down once.
        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Sand Throw"));
        var target = combatants.Single(c => !c.IsPlayer);
        Assert.Equal(1, target.ActiveConditions[ConditionType.Blinded]);

        Resolve(engine, combatants, new UseAbilityAction(monster.CreatureId, "Strike"));

        // Assert — expired after its duration elapsed
        Assert.Equal(0, target.ActiveConditions[ConditionType.Blinded]);
    }

    [Fact]
    public void ProcessRound_RefreshesExistingBuff_InsteadOfStacking_WhenSameAbilityReapplied()
    {
        // Arrange
        var buffAbility = Builders.MakeBuffSupportAbility(name: "Battle Stance");
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
        Assert.Equal(5, playerState.Strength);
    }

    [Fact]
    public void ApplyBuff_AllowsDifferentNamedBuffs_ToCoexist()
    {
        // Arrange
        var battleStance = Builders.MakeBuffSupportAbility("Battle Stance");
        var ironWill = Builders.MakeBuffSupportAbility(
            "Iron Will",
            attribute: AttributeName.Defense,
            amount: 10
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
    public void ProcessRound_Block_AppliesDefenseBuff_WhenParryCapable()
    {
        // Arrange — a melee weapon makes the caster parry-capable, so Block doubles Defense
        var weapon = Builders.MakeWeapon(_worldId);
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithDefense(10)
            .WithAbilities(BlockStance)
            .WithItem(weapon)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Block"));

        // Assert — Defense is doubled via the buff, not a bespoke damage-mitigation path
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Equal(20, playerState.Defense);
        Assert.Contains(
            playerState.ActiveBuffs,
            b => b is { AbilityName: "Block", Attribute: AttributeName.Defense }
        );
    }

    [Fact]
    public void ProcessRound_Block_AppliesPhysicalResistance_WhenNotParryCapable()
    {
        // Arrange — no shield or melee weapon equipped, so Block braces instead of parrying
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(BlockStance)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Block"));

        // Assert
        var playerState = combatants.Single(c => c.IsPlayer);
        Assert.Contains(
            playerState.ActiveBuffs,
            b => b is { AbilityName: "Block", Attribute: AttributeName.PhysicalResistance }
        );
    }

    [Fact]
    public void ProcessRound_Block_DeductsItsApCost()
    {
        // Arrange — player starts at full AP, so this round's regen is a no-op and only the
        // ability's own cost should change CurrentAp
        var player = MakeCombatant("Hero")
            .AsPlayer()
            .WithDexterity(20)
            .WithAbilities(BlockStance)
            .Build();
        var monster = MakeCombatant("Wraith").WithAbilities(MakeAttack()).Build();
        IReadOnlyList<Combatant> combatants = [player, monster];
        var engine = MakeEngine(AlwaysMiss);

        // Act
        var state = Resolve(engine, combatants, new UseAbilityAction(player.CreatureId, "Block"));

        // Assert
        var resourceState = Assert.Single(
            state.Events.OfType<ResourceStateUpdated>(),
            update => update.CombatantId == player.CreatureId
        );
        Assert.Equal(player.MaximumAp - BlockStance.ApCost, resourceState.CurrentAp);
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
            .WithAbilities(Builders.MakeHealSupportAbility(amount: 999))
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
    public void ProcessRound_DelegatesEnemyTurnToEnemyCombatActionResolver()
    {
        // Arrange — the AI decision itself is covered by EnemyCombatActionResolverTests; this
        // only proves CombatEngine actually wires that resolver's decision into the round
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
        var consumed = Assert.IsType<ConsumedPotion>(
            Assert.Single(state.Events, e => e is ConsumedPotion)
        );
        Assert.Equal(ResourceType.Hp, consumed.Resource);
    }
}
