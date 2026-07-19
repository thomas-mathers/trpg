using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using ActiveBuff = TRPG.Application.Creatures.ActiveBuff;

namespace TRPG.Application.Combat;

public class CombatEngine(
    IOptionsSnapshot<CombatOptions> optionsSnapshot,
    HitCalculator hitCalculator,
    DamageCalculator damageCalculator
)
{
    public CombatState ProcessRound(
        IReadOnlyList<Combatant> combatants,
        string abilityName,
        string targetName
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var (ability, targets) = ResolvePlayerAction(combatants, abilityName, targetName);

        var turnOrder = combatants
            .OrderByDescending(c => c.CalculateEffectiveAttribute(AttributeName.Dexterity))
            .ToArray();

        var combatEvents = turnOrder
            .SelectMany(combatant =>
                combatant == player
                    ? ProcessTurn(player, ability, targets)
                    : ProcessTurn(combatant, ChooseEnemyAbility(combatant), [player])
            )
            .ToArray();

        var outcome = GetCurrentOutcome(player, enemies);

        return new CombatState(
            Outcome: outcome,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: combatEvents,
            XpGained: outcome == CombatOutcome.Victory
                ? GetTotalExperienceFromEnemies(enemies)
                : null,
            GoldLooted: outcome == CombatOutcome.Victory ? GetTotalGoldFromEnemies(enemies) : null,
            WeaponSwingCounts: player.WeaponSwingCounts
        );
    }

    private static (Ability, IReadOnlyList<Combatant>) ResolvePlayerAction(
        IReadOnlyList<Combatant> combatants,
        string abilityName,
        string targetName
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var ability = player.Abilities.FirstOrDefault(x => x.Name == abilityName);
        if (ability is null)
        {
            throw new ArgumentException($"Ability {abilityName} not found", nameof(abilityName));
        }

        var target = combatants.FirstOrDefault(x => x.Name == targetName);
        if (target is null)
        {
            throw new ArgumentException($"Target {targetName} not found", nameof(targetName));
        }

        var cooldownRemaining = player.CooldownRemainingByAbility[abilityName];
        if (cooldownRemaining > 0)
        {
            throw new InvalidOperationException(
                $"Ability {abilityName} is on cooldown for {cooldownRemaining} more rounds"
            );
        }

        switch (ability)
        {
            case SupportAbility when target != player:
                throw new InvalidOperationException(
                    $"Ability {abilityName} can only be cast on {player.Name}"
                );
            case AttackAbility when target == player:
                throw new InvalidOperationException(
                    $"Ability {abilityName} cannot target {player.Name}"
                );
        }

        if (!target.IsAlive)
        {
            throw new InvalidOperationException($"Target {targetName} is already dead");
        }

        if (player.CurrentAp < ability.ApCost)
        {
            throw new InvalidOperationException(
                $"Ability {abilityName} costs {ability.ApCost} AP but {player.Name} only has {player.CurrentAp}"
            );
        }

        if (player.CurrentMp < ability.MpCost)
        {
            throw new InvalidOperationException(
                $"Ability {abilityName} costs {ability.MpCost} MP but {player.Name} only has {player.CurrentMp}"
            );
        }

        var targets = ability switch
        {
            AttackAbility { TargetType: AttackTargetType.Aoe } => enemies
                .Where(e => e.IsAlive)
                .ToArray(),
            SupportAbility { TargetType: TargetType.Aoe } => [player],
            _ => [target],
        };

        return (ability, targets);
    }

    private List<CombatEvent> ProcessTurn(
        Combatant actor,
        Ability ability,
        IReadOnlyList<Combatant> targets
    )
    {
        if (!actor.IsAlive)
        {
            return [];
        }

        var hotEvents = ProcessHotTicks(actor);
        var dotEvents = ProcessDotTicks(actor);
        var tickEvents = hotEvents.Concat(dotEvents).ToList();

        if (!actor.IsAlive)
        {
            return tickEvents;
        }

        RegenerateResources(actor);
        TickCooldowns(actor);

        var incapacitationEvent = GetIncapacitationEvent(actor, ability);
        if (incapacitationEvent is not null)
        {
            return tickEvents.Concat([incapacitationEvent]).ToList();
        }

        actor.CooldownRemainingByAbility[ability.Name] = ability.Cooldown;
        actor.CurrentAp -= ability.ApCost;
        actor.CurrentMp -= ability.MpCost;

        var actionEvents = ability switch
        {
            InstantHealAbility heal => ApplyInstantHeal(actor, heal, targets),
            HealOverTimeAbility hot => ApplyHealOverTime(actor, hot, targets),
            BuffAbility buff => ApplyBuff(actor, buff, targets),
            AttackAbility attack => ApplyAttack(actor, attack, targets),
            _ => [],
        };

        return tickEvents.Concat(actionEvents).ToList();
    }

    private void RegenerateResources(Combatant actor)
    {
        var apRegenAmount = Math.Max(
            1,
            (int)Math.Round(actor.MaximumAp * optionsSnapshot.Value.ApRegenPercentPerRound)
        );
        var mpRegenAmount = Math.Max(
            1,
            (int)Math.Round(actor.MaximumMp * optionsSnapshot.Value.MpRegenPercentPerRound)
        );
        actor.CurrentAp = Math.Min(actor.CurrentAp + apRegenAmount, actor.MaximumAp);
        actor.CurrentMp = Math.Min(actor.CurrentMp + mpRegenAmount, actor.MaximumMp);
    }

    private static void TickCooldowns(Combatant actor)
    {
        foreach (var abilityName in actor.CooldownRemainingByAbility.Keys)
        {
            actor.CooldownRemainingByAbility[abilityName] = Math.Max(
                0,
                actor.CooldownRemainingByAbility[abilityName] - 1
            );
        }
    }

    private static List<CombatEvent> ApplyInstantHeal(
        Combatant actor,
        InstantHealAbility ability,
        IReadOnlyList<Combatant> targets
    )
    {
        var combatEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            target.CurrentHp = Math.Min(target.CurrentHp + ability.Amount, target.MaximumHp);

            combatEvents.Add(
                new Healed(
                    actor.Name,
                    ability.Name,
                    target.Name,
                    ability.Amount,
                    target.CurrentHp,
                    target.MaximumHp
                )
            );
        }

        return combatEvents;
    }

    private static List<CombatEvent> ApplyHealOverTime(
        Combatant actor,
        HealOverTimeAbility ability,
        IReadOnlyList<Combatant> targets
    )
    {
        var combatEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            target.ActiveHots.Add(
                new ActiveHot
                {
                    AbilityName = ability.Name,
                    Amount = ability.AmountPerTurn,
                    RemainingTurns = ability.Duration,
                }
            );

            combatEvents.Add(
                new HealOverTimeApplied(
                    actor.Name,
                    ability.Name,
                    target.Name,
                    ability.AmountPerTurn,
                    ability.Duration
                )
            );
        }

        return combatEvents;
    }

    private static List<CombatEvent> ApplyBuff(
        Combatant actor,
        BuffAbility ability,
        IReadOnlyList<Combatant> targets
    )
    {
        var buffEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            var buffs = new List<BuffModifierInfo>();

            foreach (var modifier in ability.Modifiers)
            {
                target.ActiveBuffs.Add(
                    new ActiveBuff
                    {
                        Amount = modifier.Amount,
                        AmountType = modifier.AmountType,
                        Attribute = modifier.Attribute,
                        RemainingTurns = ability.Duration,
                    }
                );

                buffs.Add(
                    new BuffModifierInfo(
                        modifier.Amount,
                        modifier.AmountType,
                        modifier.Attribute,
                        ability.Duration
                    )
                );
            }

            buffEvents.Add(new BuffApplied(actor.Name, ability.Name, target.Name, buffs));
        }

        return buffEvents;
    }

    private List<CombatEvent> ApplyAttack(
        Combatant attacker,
        AttackAbility ability,
        IReadOnlyList<Combatant> defenders
    )
    {
        if (ability.DamageType == DamageType.Physical && attacker.Weapon is { } weapon)
        {
            attacker.WeaponSwingCounts[weapon.Type] =
                attacker.WeaponSwingCounts.GetValueOrDefault(weapon.Type) + 1;
        }

        var combatEvents = new List<CombatEvent>();

        foreach (var defender in defenders)
        {
            var didHit = hitCalculator.DidHit(attacker, ability, defender);

            if (!didHit)
            {
                combatEvents.Add(new Miss(attacker.Name, ability.Name, defender.Name));
                continue;
            }

            var didBlock = hitCalculator.DidBlock(ability, defender);

            if (didBlock)
            {
                combatEvents.Add(new Block(attacker.Name, ability.Name, defender.Name));
                continue;
            }

            var damage = damageCalculator.CalculateDamage(attacker, ability, defender);

            defender.CurrentHp = Math.Max(defender.CurrentHp - damage, 0);

            foreach (var dot in ability.Dots)
            {
                defender.ActiveDots.Add(
                    new ActiveDot
                    {
                        AbilityName = ability.Name,
                        Amount =
                            dot.AmountType == AmountType.Percent
                                ? (int)Math.Round(defender.MaximumHp * dot.Amount)
                                : (int)Math.Round(dot.Amount),
                        DamageType = ability.DamageType,
                        RemainingTurns = dot.Duration,
                    }
                );
            }

            var appliedConditions = new List<ConditionType>();

            foreach (var status in ability.Conditions)
            {
                defender.ActiveConditions[status.Condition] = status.Duration;
                appliedConditions.Add(status.Condition);
            }

            combatEvents.Add(
                new Hit(
                    attacker.Name,
                    ability.Name,
                    defender.Name,
                    defender.CurrentHp,
                    defender.MaximumHp,
                    !defender.IsAlive,
                    damage,
                    ability.DamageType,
                    appliedConditions
                )
            );
        }

        return combatEvents;
    }

    private static List<CombatEvent> ProcessHotTicks(Combatant actor)
    {
        var healEvents = new List<CombatEvent>();

        foreach (var hot in actor.ActiveHots.Where(hot => hot.RemainingTurns > 0))
        {
            hot.RemainingTurns--;

            actor.CurrentHp = Math.Min(actor.CurrentHp + hot.Amount, actor.MaximumHp);

            healEvents.Add(
                new Healed(
                    actor.Name,
                    hot.AbilityName,
                    actor.Name,
                    hot.Amount,
                    actor.CurrentHp,
                    actor.MaximumHp
                )
            );
        }

        actor.ActiveHots.RemoveAll(hot => hot.RemainingTurns == 0);

        return healEvents;
    }

    private List<CombatEvent> ProcessDotTicks(Combatant defender)
    {
        var damageTickedEvents = new List<CombatEvent>();

        foreach (var dot in defender.ActiveDots.Where(dot => dot.RemainingTurns > 0))
        {
            dot.RemainingTurns--;

            var damage = damageCalculator.CalculateDamage(dot.Amount, dot.DamageType, defender);

            defender.CurrentHp = Math.Max(defender.CurrentHp - damage, 0);

            damageTickedEvents.Add(
                new DamageTicked(
                    defender.Name,
                    dot.AbilityName,
                    dot.DamageType,
                    damage,
                    defender.CurrentHp,
                    defender.MaximumHp,
                    !defender.IsAlive
                )
            );

            if (!defender.IsAlive)
            {
                break;
            }
        }

        defender.ActiveDots.RemoveAll(dot => dot.RemainingTurns == 0);

        return damageTickedEvents;
    }

    private static CombatEvent? GetIncapacitationEvent(Combatant attacker, Ability ability)
    {
        var frozenTurnsRemaining = attacker.ActiveConditions[ConditionType.Frozen];
        if (frozenTurnsRemaining > 0)
        {
            return new NoAction(attacker.Name, ConditionType.Frozen);
        }

        var stunnedTurnsRemaining = attacker.ActiveConditions[ConditionType.Stunned];
        if (stunnedTurnsRemaining > 0)
        {
            return new NoAction(attacker.Name, ConditionType.Stunned);
        }

        var blindedTurnsRemaining = attacker.ActiveConditions[ConditionType.Blinded];
        if (
            blindedTurnsRemaining > 0
            && ability is AttackAbility { DamageType: DamageType.Physical }
        )
        {
            return new NoAction(attacker.Name, ConditionType.Blinded);
        }

        var silencedTurnsRemaining = attacker.ActiveConditions[ConditionType.Silenced];
        if (
            silencedTurnsRemaining > 0
            && ability
                is AttackAbility
                {
                    DamageType: DamageType.Fire
                        or DamageType.Ice
                        or DamageType.Lightning
                        or DamageType.Poison
                        or DamageType.Magic
                }
        )
        {
            return new NoAction(attacker.Name, ConditionType.Silenced);
        }

        return null;
    }

    private static Ability ChooseEnemyAbility(Combatant enemy)
    {
        var affordableAbilities = enemy.Abilities.Where(a =>
            enemy.CooldownRemainingByAbility[a.Name] == 0
            && enemy.CurrentAp >= a.ApCost
            && enemy.CurrentMp >= a.MpCost
        );

        var bestAttackAbility = affordableAbilities
            .OfType<AttackAbility>()
            .MaxBy(a =>
                a.DamageType == DamageType.Physical ? a.DamageAmount / 100f : a.DamageAmount
            );

        return bestAttackAbility ?? enemy.Abilities[0];
    }

    private static CombatOutcome GetCurrentOutcome(
        Combatant player,
        IReadOnlyList<Combatant> enemies
    )
    {
        if (!player.IsAlive)
        {
            return CombatOutcome.Defeat;
        }

        var allEnemiesKilled = enemies.All(e => !e.IsAlive);

        if (allEnemiesKilled)
        {
            return CombatOutcome.Victory;
        }

        return CombatOutcome.Ongoing;
    }

    private int GetTotalExperienceFromEnemies(IReadOnlyList<Combatant> enemies) =>
        enemies.Sum(e => e.Level * optionsSnapshot.Value.XpPerEnemyLevel);

    private static int GetTotalGoldFromEnemies(IReadOnlyList<Combatant> enemies) =>
        enemies.Sum(e => e.Gold);

    public CombatState ResolveFlee(IReadOnlyList<Combatant> combatants)
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var hotEvents = ProcessHotTicks(player);
        var dotEvents = ProcessDotTicks(player);
        var combatEvents = hotEvents.Concat(dotEvents).ToList();

        if (player.IsAlive)
        {
            var enemyTurnOrder = enemies
                .Where(e => e.IsAlive)
                .OrderByDescending(e => e.CalculateEffectiveAttribute(AttributeName.Dexterity));

            foreach (var enemy in enemyTurnOrder)
            {
                combatEvents.AddRange(ProcessTurn(enemy, ChooseEnemyAbility(enemy), [player]));
            }
        }

        var outcome = player.IsAlive ? CombatOutcome.Fled : CombatOutcome.Defeat;

        return new CombatState(
            Outcome: outcome,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: combatEvents,
            XpGained: null,
            GoldLooted: null,
            WeaponSwingCounts: player.WeaponSwingCounts
        );
    }
}
