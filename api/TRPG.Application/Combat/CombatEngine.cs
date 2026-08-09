using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Common.Extensions;
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

        var turnOrder = combatants.OrderByTurnOrder();

        var combatEvents = turnOrder
            .SelectMany(combatant =>
                combatant == player
                    ? ProcessTurn(player, action)
                    : ProcessTurn(combatant, enemyCombatActionResolver.Resolve(combatant, player))
            )
            .ToList();

        foreach (var combatant in combatants.Where(c => c.IsAlive))
        {
            var regeneration = TickInCombatResourceRegeneration(combatant);
            if (regeneration is not null)
            {
                combatEvents.Add(regeneration);
            }
        }

        var outcome = GetCurrentOutcome(player, enemies);

        return new CombatState(
            Outcome: outcome,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: combatEvents,
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

    private List<CombatEvent> ProcessAbility(
        Combatant actor,
        ResolvedUseAbilityAction resolvedUseAbilityAction
    )
    {
        var (ability, targets) = resolvedUseAbilityAction;

        actor.CooldownRemainingByAbility[ability.Name] = ability.Cooldown;
        actor.CurrentAp -= ability.ApCost;
        actor.CurrentMp -= ability.MpCost;

        var resourceState = new ResourceStateUpdated(
            actor.CreatureId,
            actor.Name,
            actor.CurrentAp,
            actor.MaximumAp,
            actor.CurrentMp,
            actor.MaximumMp
        );

        var trainedSkill = ResolveTrainedSkill(actor, ability);

        actor.SkillUsageCounts[trainedSkill] =
            actor.SkillUsageCounts.GetValueOrDefault(trainedSkill) + 1;

        var actionEvents = ability switch
        {
            SupportAbility support => ApplySupport(actor, support, targets),
            AttackAbility attack => ApplyAttack(actor, attack, targets),
            _ => [],
        };

        return [resourceState, .. actionEvents];
    }

    private static Skill ResolveTrainedSkill(Combatant actor, Ability ability)
    {
        if (ability == AbilityCatalog.Strike)
        {
            return actor.MainHandWeapon is { } weapon ? GetWeaponSkill(weapon.Type) : Skill.Unarmed;
        }

        return ability.Skill;
    }

    private static Skill GetWeaponSkill(WeaponType weaponType) =>
        weaponType switch
        {
            WeaponType.Sword => Skill.Melee,
            WeaponType.Dagger => Skill.Melee,
            WeaponType.Axe => Skill.Melee,
            WeaponType.Mace => Skill.Melee,
            WeaponType.Hammer => Skill.Melee,
            WeaponType.GreatSword => Skill.Melee,
            WeaponType.GreatAxe => Skill.Melee,
            WeaponType.GreatHammer => Skill.Melee,
            WeaponType.Bow => Skill.Archery,
            WeaponType.Crossbow => Skill.Archery,
            WeaponType.Javelin => Skill.Archery,
            WeaponType.Staff => Skill.Destruction,
            WeaponType.Wand => Skill.Destruction,
            _ => throw new ArgumentOutOfRangeException(nameof(weaponType), weaponType, null),
        };

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

    private Regenerated? TickInCombatResourceRegeneration(Combatant actor)
    {
        var currentAp = actor.CurrentAp;
        var currentMp = actor.CurrentMp;
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

        return actor.CurrentAp == currentAp && actor.CurrentMp == currentMp
            ? null
            : new Regenerated(
                actor.CreatureId,
                actor.Name,
                currentAp,
                actor.CurrentAp,
                actor.MaximumAp,
                currentMp,
                actor.CurrentMp,
                actor.MaximumMp
            );
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

    private static List<CombatEvent> ApplySupport(
        Combatant actor,
        SupportAbility ability,
        IReadOnlyList<Combatant> targets
    )
    {
        var buffs =
            ability.BuffsWhileParrying.Count > 0 && AbilityGearRequirement.IsParryCapable(actor)
                ? ability.BuffsWhileParrying
                : ability.Buffs;

        var combatEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            if (ability.HealAmount > 0)
            {
                combatEvents.Add(ApplyHeal(actor, ability, target));
            }

            combatEvents.AddRange(
                ability.Hots.Select(hot => ApplyHot(actor, ability.Name, hot, target))
            );

            if (buffs.Count > 0)
            {
                combatEvents.Add(ApplyBuffs(actor, ability.Name, buffs, target));
            }
        }

        return combatEvents;
    }

    private static CombatEvent ApplyHeal(Combatant actor, SupportAbility ability, Combatant target)
    {
        var amount =
            ability.HealAmountType == AmountType.Percent
                ? (int)Math.Round(target.MaximumHp * ability.HealAmount)
                : (int)Math.Round(ability.HealAmount);

        target.CurrentHp = Math.Min(target.CurrentHp + amount, target.MaximumHp);

        return new Healed(
            actor.Name,
            ability.Name,
            target.Name,
            amount,
            target.CurrentHp,
            target.MaximumHp
        );
    }

    private static CombatEvent ApplyHot(
        Combatant actor,
        string abilityName,
        HotEffect hot,
        Combatant target
    )
    {
        var amountPerTurn =
            hot.AmountType == AmountType.Percent
                ? (int)Math.Round(target.MaximumHp * hot.Amount)
                : (int)Math.Round(hot.Amount);

        target.ActiveHots.RemoveAll(h => h.AbilityName == abilityName);
        target.ActiveHots.Add(
            new ActiveHot
            {
                AbilityName = abilityName,
                Amount = amountPerTurn,
                RemainingTurns = hot.Duration,
            }
        );

        return new HealOverTimeApplied(
            actor.Name,
            abilityName,
            target.Name,
            amountPerTurn,
            hot.Duration
        );
    }

    private static CombatEvent ApplyBuffs(
        Combatant actor,
        string abilityName,
        IReadOnlyList<AttributeEffect> buffs,
        Combatant target
    )
    {
        var appliedModifiers = new List<BuffModifierInfo>();

        foreach (var buff in buffs)
        {
            target.ActiveBuffs.RemoveAll(b =>
                b.AbilityName == abilityName && b.Attribute == buff.Attribute
            );
            target.ActiveBuffs.Add(
                new ActiveBuff
                {
                    AbilityName = abilityName,
                    Amount = buff.Amount,
                    AmountType = buff.AmountType,
                    Attribute = buff.Attribute,
                    RemainingTurns = buff.Duration,
                }
            );

            appliedModifiers.Add(
                new BuffModifierInfo(buff.Amount, buff.AmountType, buff.Attribute, buff.Duration)
            );
        }

        return new BuffApplied(actor.Name, abilityName, target.Name, appliedModifiers);
    }

    private List<CombatEvent> ApplyAttack(
        Combatant attacker,
        AttackAbility ability,
        IReadOnlyList<Combatant> defenders
    )
    {
        var mainHandWeapon = attacker.MainHandWeapon;
        var offHandWeapon = attacker.OffHandWeapon;
        var mainHandBonusSwings = Math.Max(0, (mainHandWeapon?.AttacksPerTurn ?? 1) - 1);
        var offHandSwings = offHandWeapon?.AttacksPerTurn ?? 0;

        List<AttackAbility> mainHandAbilities =
        [
            ability,
            .. Enumerable.Repeat(AbilityCatalog.Strike, mainHandBonusSwings),
        ];
        List<AttackAbility> offHandAbilities =
        [
            .. Enumerable.Repeat(AbilityCatalog.Strike, offHandSwings),
        ];

        var combatEvents = new List<CombatEvent>();

        foreach (var defender in defenders)
        {
            combatEvents.AddRange(
                mainHandAbilities.Select(a =>
                    ResolveWeaponSwing(attacker, a, defender, mainHandWeapon)
                )
            );
            combatEvents.AddRange(
                offHandAbilities.Select(a =>
                    ResolveWeaponSwing(attacker, a, defender, offHandWeapon)
                )
            );
        }

        return combatEvents;
    }

    private CombatEvent ResolveWeaponSwing(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon
    )
    {
        if (ability.DamageType == DamageType.Physical && weapon is { } swungWeapon)
        {
            attacker.WeaponSwingCounts[swungWeapon.Type] =
                attacker.WeaponSwingCounts.GetValueOrDefault(swungWeapon.Type) + 1;
        }

        var didHit = hitCalculator.RollHit(attacker, ability, defender, weapon);

        if (!didHit)
        {
            return new Miss(
                AttackerId: attacker.CreatureId,
                AttackerName: attacker.Name,
                AbilityName: ability.Name,
                TargetId: defender.CreatureId,
                TargetName: defender.Name
            );
        }

        var didBlock = hitCalculator.RollBlock(ability, defender);

        if (didBlock)
        {
            defender.SkillUsageCounts[Skill.Blocking] =
                defender.SkillUsageCounts.GetValueOrDefault(Skill.Blocking) + 1;
            return new Block(
                AttackerId: attacker.CreatureId,
                AttackerName: attacker.Name,
                AbilityName: ability.Name,
                TargetId: defender.CreatureId,
                TargetName: defender.Name
            );
        }

        var damageResult = damageCalculator.CalculateDamage(attacker, ability, defender, weapon);
        var damage = damageResult.Amount;

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

        foreach (var debuff in ability.Debuffs)
        {
            defender.ActiveBuffs.RemoveAll(b =>
                b.AbilityName == ability.Name && b.Attribute == debuff.Attribute
            );
            defender.ActiveBuffs.Add(
                new ActiveBuff
                {
                    AbilityName = ability.Name,
                    Amount = debuff.Amount,
                    AmountType = debuff.AmountType,
                    Attribute = debuff.Attribute,
                    RemainingTurns = debuff.Duration,
                }
            );
        }

        return new Hit(
            AttackerId: attacker.CreatureId,
            AttackerName: attacker.Name,
            AbilityName: ability.Name,
            TargetId: defender.CreatureId,
            TargetName: defender.Name,
            TargetRemainingHp: defender.CurrentHp,
            TargetMaximumHp: defender.MaximumHp,
            Killed: !defender.IsAlive,
            IsCritical: damageResult.IsCritical,
            Damage: damage,
            DamageType: ability.DamageType,
            AppliedConditions: appliedConditions
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
            TickCooldowns(actor);
        }

        return tickEvents;
    }

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

    public CombatState ResolveFlee(IReadOnlyList<Combatant> combatants)
    {
        var player = combatants.Single(c => c.IsPlayer);

        return new CombatState(
            Outcome: CombatOutcome.Fled,
            Combatants: combatants.Select(c => c.ToCombatantState()).ToArray(),
            Events: [],
            WeaponSwingCounts: player.WeaponSwingCounts,
            SkillUsageCounts: player.SkillUsageCounts
        );
    }
}

internal static class CombatantExtensions
{
    public static IReadOnlyList<Combatant> OrderByTurnOrder(this IEnumerable<Combatant> combatants)
    {
        return combatants.Shuffled().OrderByDescending(c => c.TurnOrder).ToArray();
    }
}
