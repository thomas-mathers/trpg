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
    DamageCalculator damageCalculator,
    EnemyCombatActionResolver enemyCombatActionResolver
)
{
    internal CombatState ProcessRound(
        IReadOnlyList<Combatant> combatants,
        ResolvedCombatAction action
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        // Shuffled before the (stable) sort so a Dexterity tie doesn't always resolve in favor
        // of whichever combatant happened to be listed first - without this, ties are a
        // structural, repeatable bias rather than a genuine coin flip.
        var turnOrder = combatants.ToArray();
        Random.Shared.Shuffle(turnOrder);
        turnOrder = turnOrder.OrderByDescending(c => c.TurnOrderValue).ToArray();

        var combatEvents = turnOrder
            .SelectMany(combatant =>
                combatant == player
                    ? ProcessTurn(player, action)
                    : ProcessTurn(combatant, enemyCombatActionResolver.Resolve(combatant, player))
            )
            .ToArray();

        var outcome = GetCurrentOutcome(player, enemies);

        return new CombatState(
            Outcome: outcome,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: combatEvents,
            GoldLooted: outcome == CombatOutcome.Victory ? GetTotalGoldFromEnemies(enemies) : null,
            WeaponSwingCounts: player.WeaponSwingCounts,
            SkillUsageCounts: player.SkillUsageCounts
        );
    }

    private List<CombatEvent> ProcessTurn(Combatant actor, ResolvedCombatAction action)
    {
        if (!actor.IsAlive)
        {
            return [];
        }

        var tickEvents = ProcessTicks(actor);

        if (!actor.IsAlive)
        {
            return tickEvents;
        }

        var incapacitationEvent = GetIncapacitationEvent(actor, action);

        // Ticked after the check above reads it, not before (unlike buffs/dots/hots, which apply
        // their own effect as part of their own tick) - a condition set to Duration=1 needs to
        // still be read as active for the one turn it's meant to block, then expire afterward.
        TickConditions(actor);

        if (incapacitationEvent is not null)
        {
            return tickEvents.Concat([incapacitationEvent]).ToList();
        }

        var actionEvents = action switch
        {
            ResolvedUseAbilityAction resolved => ProcessAbility(actor, resolved),
            ResolvedUseItemAction resolved => ProcessItem(actor, resolved.Item),
            _ => [],
        };

        return tickEvents.Concat(actionEvents).ToList();
    }

    // A General-skill attack is the weaponless "Strike" template — its training goes to the
    // wielded weapon's tree (or Unarmed), not to General itself. Named abilities keep their
    // inherent skill.
    private static Skill GetTrainedSkill(Combatant actor, Ability ability)
    {
        if (ability is not AttackAbility || ability.Skill != Skill.General)
        {
            return ability.Skill;
        }

        return actor.Weapon is { } weapon
            ? AbilityGearRequirement.WeaponSkills.GetValueOrDefault(weapon.Type, Skill.General)
            : Skill.Unarmed;
    }

    private List<CombatEvent> ProcessAbility(
        Combatant actor,
        ResolvedUseAbilityAction resolvedUseAbilityAction
    )
    {
        var (ability, targets) = resolvedUseAbilityAction;

        actor.CooldownRemainingByAbility[ability.Name] = ability.Cooldown;
        actor.CurrentAp -= ability.ApCost;
        actor.CurrentMp -= ability.MpCost;

        var trainedSkill = GetTrainedSkill(actor, ability);
        actor.SkillUsageCounts[trainedSkill] =
            actor.SkillUsageCounts.GetValueOrDefault(trainedSkill) + 1;

        return ability switch
        {
            InstantHealAbility heal => ApplyInstantHeal(actor, heal, targets),
            HealOverTimeAbility hot => ApplyHealOverTime(actor, hot, targets),
            BuffAbility buff => ApplyBuff(actor, buff, targets),
            AttackAbility attack => ApplyAttack(actor, attack, targets),
            _ => [],
        };
    }

    private static List<CombatEvent> ProcessItem(Combatant actor, ConsumableItemSnapshot item)
    {
        actor.ItemsUsedCounts[item.ItemId] =
            actor.ItemsUsedCounts.GetValueOrDefault(item.ItemId) + 1;

        var (currentValue, maximumValue) = item.Resource switch
        {
            ResourceType.Hp => (actor.CurrentHp, actor.MaximumHp),
            ResourceType.Ap => (actor.CurrentAp, actor.MaximumAp),
            ResourceType.Mp => (actor.CurrentMp, actor.MaximumMp),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

        var remainingValue = Math.Min(currentValue + item.Amount, maximumValue);

        switch (item.Resource)
        {
            case ResourceType.Hp:
                actor.CurrentHp = remainingValue;
                break;
            case ResourceType.Ap:
                actor.CurrentAp = remainingValue;
                break;
            case ResourceType.Mp:
                actor.CurrentMp = remainingValue;
                break;
        }

        return
        [
            new ConsumedPotion(
                actor.Name,
                item.Name,
                item.Resource,
                item.Amount,
                remainingValue,
                maximumValue
            ),
        ];
    }

    private void TickInCombatResourceRegeneration(Combatant actor)
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
            target.ActiveHots.RemoveAll(h => h.AbilityName == ability.Name);
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
        var modifiers =
            ability.ParryCapableModifiers.Count > 0 && AbilityGearRequirement.IsParryCapable(actor)
                ? ability.ParryCapableModifiers
                : ability.Modifiers;

        var buffEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            var buffs = new List<BuffModifierInfo>();

            foreach (var modifier in modifiers)
            {
                target.ActiveBuffs.RemoveAll(b =>
                    b.AbilityName == ability.Name && b.Attribute == modifier.Attribute
                );
                target.ActiveBuffs.Add(
                    new ActiveBuff
                    {
                        AbilityName = ability.Name,
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

    // Only the ability itself lands on the first swing (its own DamageAmount, dots, conditions).
    // Any bonus swings from a fast weapon (see WeaponAttackSpeed) are the weapon striking again on
    // its own - independently rolled, damage-only, no extra AP/MP cost.
    private const string BonusSwingAbilityName = "Follow-up Strike";

    private List<CombatEvent> ApplyAttack(
        Combatant attacker,
        AttackAbility ability,
        IReadOnlyList<Combatant> defenders
    )
    {
        var attacksPerTurn = WeaponAttackSpeed.AttacksPerTurn(attacker.Weapon);
        var combatEvents = new List<CombatEvent>();

        foreach (var defender in defenders)
        {
            combatEvents.Add(ResolvePrimarySwing(attacker, ability, defender));

            for (var swing = 1; swing < attacksPerTurn; swing++)
            {
                combatEvents.Add(ResolveBonusSwing(attacker, defender));
            }
        }

        return combatEvents;
    }

    private CombatEvent ResolvePrimarySwing(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender
    )
    {
        if (ability.DamageType == DamageType.Physical && attacker.Weapon is { } weapon)
        {
            attacker.WeaponSwingCounts[weapon.Type] =
                attacker.WeaponSwingCounts.GetValueOrDefault(weapon.Type) + 1;
        }

        var didHit = hitCalculator.DidHit(attacker, ability, defender);

        if (!didHit)
        {
            return new Miss(attacker.Name, ability.Name, defender.Name);
        }

        var didBlock = hitCalculator.DidBlock(ability, defender);

        if (didBlock)
        {
            defender.SkillUsageCounts[Skill.Blocking] =
                defender.SkillUsageCounts.GetValueOrDefault(Skill.Blocking) + 1;
            return new Block(attacker.Name, ability.Name, defender.Name);
        }

        var damage = damageCalculator.CalculateDamage(attacker, ability, defender);

        defender.CurrentHp = Math.Max(defender.CurrentHp - damage, 0);

        foreach (var dot in ability.Dots)
        {
            defender.ActiveDots.RemoveAll(d => d.AbilityName == ability.Name);
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

        return new Hit(
            attacker.Name,
            ability.Name,
            defender.Name,
            defender.CurrentHp,
            defender.MaximumHp,
            !defender.IsAlive,
            damage,
            ability.DamageType,
            appliedConditions
        );
    }

    private CombatEvent ResolveBonusSwing(Combatant attacker, Combatant defender)
    {
        if (attacker.Weapon is { } weapon)
        {
            attacker.WeaponSwingCounts[weapon.Type] =
                attacker.WeaponSwingCounts.GetValueOrDefault(weapon.Type) + 1;
        }

        var didHit = hitCalculator.DidHitWithWeapon(attacker, defender);

        if (!didHit)
        {
            return new Miss(attacker.Name, BonusSwingAbilityName, defender.Name);
        }

        var didBlock = hitCalculator.DidBlockWeaponSwing(defender);

        if (didBlock)
        {
            defender.SkillUsageCounts[Skill.Blocking] =
                defender.SkillUsageCounts.GetValueOrDefault(Skill.Blocking) + 1;
            return new Block(attacker.Name, BonusSwingAbilityName, defender.Name);
        }

        var damage = damageCalculator.CalculateBonusSwingDamage(attacker, defender);

        defender.CurrentHp = Math.Max(defender.CurrentHp - damage, 0);

        return new Hit(
            attacker.Name,
            BonusSwingAbilityName,
            defender.Name,
            defender.CurrentHp,
            defender.MaximumHp,
            !defender.IsAlive,
            damage,
            DamageType.Physical,
            []
        );
    }

    private List<CombatEvent> ProcessTicks(Combatant actor)
    {
        var hotEvents = TickHots(actor);
        var dotEvents = TickDots(actor);

        TickBuffs(actor);

        var tickEvents = hotEvents.Concat(dotEvents).ToList();

        if (actor.IsAlive)
        {
            TickInCombatResourceRegeneration(actor);
            TickCooldowns(actor);
        }

        return tickEvents;
    }

    // Every ConditionType key is always present (Combatant initializes all of them), so this
    // just counts each one down without needing to add or remove keys.
    private static void TickConditions(Combatant actor)
    {
        foreach (var condition in actor.ActiveConditions.Keys.ToArray())
        {
            if (actor.ActiveConditions[condition] > 0)
            {
                actor.ActiveConditions[condition]--;
            }
        }
    }

    private static List<CombatEvent> TickHots(Combatant actor)
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

    private static void TickBuffs(Combatant actor)
    {
        foreach (var buff in actor.ActiveBuffs.Where(buff => buff.RemainingTurns > 0))
        {
            buff.RemainingTurns--;
        }

        actor.ActiveBuffs.RemoveAll(buff => buff.RemainingTurns == 0);
    }

    private List<CombatEvent> TickDots(Combatant defender)
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

    private static CombatEvent? GetIncapacitationEvent(
        Combatant attacker,
        ResolvedCombatAction action
    )
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

        var ability = action is ResolvedUseAbilityAction resolvedAbility
            ? resolvedAbility.Ability
            : null;

        if (ability is null)
        {
            return null;
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

    private static int GetTotalGoldFromEnemies(IReadOnlyList<Combatant> enemies) =>
        enemies.Sum(e => e.Gold);

    public CombatState ResolveFlee(IReadOnlyList<Combatant> combatants)
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var hotEvents = TickHots(player);
        var dotEvents = TickDots(player);

        TickBuffs(player);
        TickConditions(player);

        var combatEvents = hotEvents.Concat(dotEvents).ToList();

        if (player.IsAlive)
        {
            var shuffledEnemies = enemies.Where(e => e.IsAlive).ToArray();
            Random.Shared.Shuffle(shuffledEnemies);
            var enemyTurnOrder = shuffledEnemies.OrderByDescending(e => e.TurnOrderValue);

            foreach (var enemy in enemyTurnOrder)
            {
                combatEvents.AddRange(
                    ProcessTurn(enemy, enemyCombatActionResolver.Resolve(enemy, player))
                );
            }
        }

        var outcome = player.IsAlive ? CombatOutcome.Fled : CombatOutcome.Defeat;

        return new CombatState(
            Outcome: outcome,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: combatEvents,
            GoldLooted: null,
            WeaponSwingCounts: player.WeaponSwingCounts,
            SkillUsageCounts: player.SkillUsageCounts
        );
    }
}
