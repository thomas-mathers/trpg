using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Extensions;
using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;

namespace TRPG.Application.Combat;

public class CombatEngine(
    IOptionsSnapshot<CombatOptions> optionsSnapshot,
    IOptionsSnapshot<FleeOptions> fleeOptionsSnapshot,
    HitCalculator hitCalculator,
    DamageCalculator damageCalculator,
    EnemyCombatActionResolver enemyCombatActionResolver,
    EffectAdvancer effectAdvancer
)
{
    public CombatState ProcessRound(
        IReadOnlyList<Combatant> combatants,
        ResolvedCombatAction action,
        GameInstant now,
        bool isSurpriseRound = false
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        // A successful flee returns immediately below; a failed one falls through to the normal round, chasers included.
        if (action is ResolvedFleeAction && !IsFleeCaught(player, enemies))
        {
            return new CombatState(
                Outcome: CombatOutcome.Fled,
                Combatants: ToOrderedCombatantResults(combatants),
                Events: [],
                WeaponSwingCounts: player.WeaponSwingCounts,
                SkillUsageCounts: player.SkillUsageCounts
            );
        }

        var combatEvents = AdvanceEffects(combatants, now);

        combatEvents.AddRange(
            isSurpriseRound
                ? ProcessTurn(player, action, now)
                : ProcessNormalRound(combatants, player, action, now)
        );

        var outcome = GetCurrentOutcome(player, enemies);

        return new CombatState(
            Outcome: outcome,
            Combatants: ToOrderedCombatantResults(combatants),
            Events: combatEvents,
            WeaponSwingCounts: player.WeaponSwingCounts,
            SkillUsageCounts: player.SkillUsageCounts
        );
    }

    private bool IsFleeCaught(Combatant player, IReadOnlyList<Combatant> enemies)
    {
        var catchChance = EvadeChanceCalculator.CatchChance(
            fleeOptionsSnapshot.Value,
            ToEvadeParticipant(player),
            enemies.Where(e => e.IsAlive).Select(ToEvadeParticipant).ToArray()
        );
        return Random.Shared.NextDouble() < catchChance;
    }

    private static IReadOnlyList<CombatantResult> ToOrderedCombatantResults(
        IReadOnlyList<Combatant> combatants
    ) =>
        combatants
            .OrderByDescending(combatant => combatant.TurnOrder)
            .Select(combatant => combatant.ToCombatantResult())
            .ToArray();

    private List<CombatResolution> AdvanceEffects(
        IReadOnlyList<Combatant> combatants,
        GameInstant now
    ) => combatants.SelectMany(combatant => effectAdvancer.Advance(combatant, now)).ToList();

    private List<CombatResolution> ProcessNormalRound(
        IReadOnlyList<Combatant> combatants,
        Combatant player,
        ResolvedCombatAction action,
        GameInstant now
    )
    {
        var turnOrder = combatants.OrderByTurnOrder();

        var combatEvents = turnOrder
            .SelectMany(combatant =>
                combatant == player
                    ? ProcessTurn(player, action, now)
                    : ProcessTurn(
                        combatant,
                        enemyCombatActionResolver.Resolve(combatant, player, now),
                        now
                    )
            )
            .ToList();

        combatEvents.AddRange(TickRegeneration(combatants));

        return combatEvents;
    }

    private List<CombatResolution> TickRegeneration(IReadOnlyList<Combatant> combatants)
    {
        var regenerationEvents = new List<CombatResolution>();

        foreach (var combatant in combatants.Where(c => c.IsAlive))
        {
            var regeneration = TickInCombatResourceRegeneration(combatant);
            if (regeneration is not null)
            {
                regenerationEvents.Add(regeneration);
            }
        }

        return regenerationEvents;
    }

    private List<CombatResolution> ProcessTurn(
        Combatant actor,
        ResolvedCombatAction action,
        GameInstant now
    )
    {
        if (!actor.IsAlive)
        {
            return [];
        }

        var incapacitationEvent = GetIncapacitationEvent(actor, action, now);

        if (incapacitationEvent is not null)
        {
            return [incapacitationEvent];
        }

        return action switch
        {
            ResolvedUseAbilityAction resolved => ProcessAbility(actor, resolved, now),
            ResolvedUseItemAction resolved => ProcessItem(actor, resolved.Item),
            // Reached only when the flee attempt was caught — a successful escape already returned above.
            ResolvedFleeAction => [new FleeFailed(actor.CreatureId, actor.Name)],
            _ => [],
        };
    }

    private List<CombatResolution> ProcessAbility(
        Combatant actor,
        ResolvedUseAbilityAction resolvedUseAbilityAction,
        GameInstant now
    )
    {
        var (ability, targets) = resolvedUseAbilityAction;

        if (ability.Cooldown > TimeSpan.Zero)
        {
            actor.CooldownReadyAtByAbility[ability.Name] = now + ability.Cooldown;
        }

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
            SupportAbility support => ApplySupport(actor, support, targets, now),
            AttackAbility attack => ApplyAttack(actor, attack, targets, now),
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
        };

    private static List<CombatResolution> ProcessItem(Combatant actor, ConsumableItemSnapshot item)
    {
        actor.ItemsUsedCounts[item.ItemId] =
            actor.ItemsUsedCounts.GetValueOrDefault(item.ItemId) + 1;

        var (currentValue, maximumValue) = item.Resource switch
        {
            ResourceType.Hp => (actor.CurrentHp, actor.MaximumHp),
            ResourceType.Ap => (actor.CurrentAp, actor.MaximumAp),
            ResourceType.Mp => (actor.CurrentMp, actor.MaximumMp),
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
                actor.CreatureId,
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

    private static List<CombatResolution> ApplySupport(
        Combatant actor,
        SupportAbility ability,
        IReadOnlyList<Combatant> targets,
        GameInstant now
    )
    {
        var buffs =
            ability.BuffsWhileParrying.Count > 0 && AbilityGearRequirement.IsParryCapable(actor)
                ? ability.BuffsWhileParrying
                : ability.Buffs;

        var combatEvents = new List<CombatResolution>();

        foreach (var target in targets)
        {
            if (ability.HealAmount > 0)
            {
                combatEvents.Add(ApplyHeal(actor, ability, target));
            }

            combatEvents.AddRange(
                ability.Hots.Select(hot => ApplyHot(actor, ability.Name, hot, target, now))
            );

            if (buffs.Count > 0)
            {
                combatEvents.Add(ApplyBuffs(actor, ability.Name, buffs, target, now));
            }
        }

        return combatEvents;
    }

    private static CombatResolution ApplyHeal(
        Combatant actor,
        SupportAbility ability,
        Combatant target
    )
    {
        var amount =
            ability.HealAmountType == AmountType.Percent
                ? (int)Math.Round(target.MaximumHp * ability.HealAmount)
                : (int)Math.Round(ability.HealAmount);

        target.CurrentHp = Math.Min(target.CurrentHp + amount, target.MaximumHp);

        return new Healed(
            actor.CreatureId,
            actor.Name,
            ability.Name,
            target.CreatureId,
            target.Name,
            amount,
            target.CurrentHp,
            target.MaximumHp
        );
    }

    private static CombatResolution ApplyHot(
        Combatant actor,
        string abilityName,
        HotEffect hot,
        Combatant target,
        GameInstant now
    )
    {
        var amountPerTick =
            hot.AmountType == AmountType.Percent
                ? (int)Math.Round(target.MaximumHp * hot.Amount)
                : (int)Math.Round(hot.Amount);

        target.ActiveHots.RemoveAll(h => h.AbilityName == abilityName);
        target.ActiveHots.Add(
            new ActiveHot
            {
                AbilityName = abilityName,
                Amount = amountPerTick,
                NextTickAt = now + CombatTiming.Round,
                ExpiresAt = now + hot.Duration,
            }
        );

        return new HealOverTimeApplied(
            actor.CreatureId,
            actor.Name,
            abilityName,
            target.CreatureId,
            target.Name,
            amountPerTick,
            (int)hot.Duration.TotalSeconds
        );
    }

    private static CombatResolution ApplyBuffs(
        Combatant actor,
        string abilityName,
        IReadOnlyList<AttributeEffect> buffs,
        Combatant target,
        GameInstant now
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
                    ExpiresAt = now + buff.Duration,
                }
            );

            appliedModifiers.Add(
                new BuffModifierInfo(
                    buff.Amount,
                    buff.AmountType,
                    buff.Attribute,
                    (int)buff.Duration.TotalSeconds
                )
            );
        }

        return new BuffApplied(
            actor.CreatureId,
            actor.Name,
            abilityName,
            target.CreatureId,
            target.Name,
            appliedModifiers
        );
    }

    private List<CombatResolution> ApplyAttack(
        Combatant attacker,
        AttackAbility ability,
        IReadOnlyList<Combatant> defenders,
        GameInstant now
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

        var combatEvents = new List<CombatResolution>();

        foreach (var defender in defenders)
        {
            combatEvents.AddRange(
                mainHandAbilities.Select(a =>
                    ResolveWeaponSwing(attacker, a, defender, mainHandWeapon, now)
                )
            );
            combatEvents.AddRange(
                offHandAbilities.Select(a =>
                    ResolveWeaponSwing(attacker, a, defender, offHandWeapon, now)
                )
            );
        }

        return combatEvents;
    }

    private CombatResolution ResolveWeaponSwing(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon,
        GameInstant now
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

        var damageResult = damageCalculator.CalculateDamage(
            attacker,
            ability,
            defender,
            weapon,
            attacker.IsSurpriseAttacker
        );
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
                    NextTickAt = now + CombatTiming.Round,
                    ExpiresAt = now + dot.Duration,
                }
            );
        }

        var appliedConditions = new List<ConditionType>();

        foreach (var status in ability.Conditions)
        {
            defender.ActiveConditions[status.Condition] = now + status.Duration;
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
                    ExpiresAt = now + debuff.Duration,
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

    private static CombatResolution? GetIncapacitationEvent(
        Combatant attacker,
        ResolvedCombatAction action,
        GameInstant now
    )
    {
        if (attacker.IsUnder(ConditionType.Frozen, now))
        {
            return new NoAction(attacker.Name, ConditionType.Frozen);
        }

        if (attacker.IsUnder(ConditionType.Stunned, now))
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

        if (
            attacker.IsUnder(ConditionType.Blinded, now)
            && ability is AttackAbility { DamageType: DamageType.Physical }
        )
        {
            return new NoAction(attacker.Name, ConditionType.Blinded);
        }

        if (
            attacker.IsUnder(ConditionType.Silenced, now)
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

    private static EvadeParticipant ToEvadeParticipant(Combatant combatant) =>
        new(
            combatant.Dexterity,
            combatant.CurrentHp,
            combatant.MaximumHp,
            combatant.CurrentAp,
            combatant.MaximumAp
        );
}

internal static class CombatantExtensions
{
    public static IReadOnlyList<Combatant> OrderByTurnOrder(this IEnumerable<Combatant> combatants)
    {
        return combatants.Shuffled().OrderByDescending(c => c.TurnOrder).ToArray();
    }
}
