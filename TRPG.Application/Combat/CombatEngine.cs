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
    internal CombatState ProcessRound(
        IReadOnlyList<Combatant> combatants,
        ResolvedCombatAction action
    )
    {
        var player = combatants.Single(c => c.IsPlayer);
        var enemies = combatants.Where(c => !c.IsPlayer).ToArray();

        var turnOrder = combatants
            .OrderByDescending(c => c.CalculateEffectiveAttribute(AttributeName.Dexterity))
            .ToArray();

        var combatEvents = turnOrder
            .SelectMany(combatant =>
                combatant == player
                    ? ProcessTurn(player, action)
                    : ProcessTurn(combatant, ResolveEnemyAction(combatant, player))
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

    private static readonly Dictionary<WeaponType, Skill> WeaponSkills = new()
    {
        [WeaponType.Sword] = Skill.Melee,
        [WeaponType.Dagger] = Skill.Melee,
        [WeaponType.Axe] = Skill.Melee,
        [WeaponType.Mace] = Skill.Melee,
        [WeaponType.Bow] = Skill.Archery,
        [WeaponType.Staff] = Skill.Spellcasting,
        [WeaponType.Wand] = Skill.Spellcasting,
    };

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
            ? WeaponSkills.GetValueOrDefault(weapon.Type, Skill.General)
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
        var buffEvents = new List<CombatEvent>();

        foreach (var target in targets)
        {
            var buffs = new List<BuffModifierInfo>();

            foreach (var modifier in ability.Modifiers)
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

    private ResolvedCombatAction ResolveEnemyAction(Combatant enemy, Combatant player)
    {
        var affordableAbilities = enemy
            .Abilities.Where(a =>
                enemy.CooldownRemainingByAbility[a.Name] == 0
                && enemy.CurrentAp >= a.ApCost
                && enemy.CurrentMp >= a.MpCost
            )
            .ToArray();

        var defensiveAction = ResolveEnemyDefensiveAction(enemy, affordableAbilities);
        if (defensiveAction is not null)
        {
            return defensiveAction;
        }

        // Every combatant always has the basic attack (0 AP/MP cost, 0 cooldown), so it's
        // always in affordableAbilities and MaxBy can never return null here.
        var bestAttackAbility = affordableAbilities
            .OfType<AttackAbility>()
            .MaxBy(a =>
                a.DamageType == DamageType.Physical ? a.DamageAmount / 100f : a.DamageAmount
            );

        return new ResolvedUseAbilityAction(bestAttackAbility!, [player]);
    }

    // A monster prioritizes surviving over anything else. While healthy it opens with any
    // unused long-running buff (e.g. a skill-tree buff) and only reaches for AP/MP potions
    // once nothing else is going on. Once hurt, a heal ability beats a health potion (no
    // inventory cost); a single-turn stance like Block is only used as a last resort once
    // there's nothing left to heal with, rather than eating a hit unguarded — never as an
    // opening move before any damage was taken.
    private ResolvedCombatAction? ResolveEnemyDefensiveAction(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    )
    {
        var threshold = optionsSnapshot.Value.LowResourceThresholdPercent;

        if (IsLow(enemy.CurrentHp, enemy.MaximumHp, threshold))
        {
            var healAbility = affordableAbilities.FirstOrDefault(a =>
                a is InstantHealAbility or HealOverTimeAbility
            );
            if (healAbility is not null)
            {
                return new ResolvedUseAbilityAction(healAbility, [enemy]);
            }

            var hpPotion = FindPotion(enemy, ResourceType.Hp);
            if (hpPotion is not null)
            {
                return new ResolvedUseItemAction(hpPotion);
            }

            var stance = FindUsableStance(enemy, affordableAbilities);
            return stance is not null ? new ResolvedUseAbilityAction(stance, [enemy]) : null;
        }

        var apPotion = IsLow(enemy.CurrentAp, enemy.MaximumAp, threshold)
            ? FindPotion(enemy, ResourceType.Ap)
            : null;
        if (apPotion is not null)
        {
            return new ResolvedUseItemAction(apPotion);
        }

        var mpPotion = IsLow(enemy.CurrentMp, enemy.MaximumMp, threshold)
            ? FindPotion(enemy, ResourceType.Mp)
            : null;
        if (mpPotion is not null)
        {
            return new ResolvedUseItemAction(mpPotion);
        }

        var openingBuff = FindUsableOpeningBuff(enemy, affordableAbilities);
        return openingBuff is not null ? new ResolvedUseAbilityAction(openingBuff, [enemy]) : null;
    }

    // A real skill-tree buff meant to be cast once and left active for several rounds.
    private static Ability? FindUsableOpeningBuff(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    ) =>
        affordableAbilities
            .OfType<BuffAbility>()
            .FirstOrDefault(a => a.Duration > 1 && IsUnused(enemy, a));

    // A single-turn stance (Duration 1, like Block) is meant to be triggered situationally
    // rather than left running, unlike a real skill-tree buff — never an interchangeable
    // candidate for the opening-move decision above.
    private static Ability? FindUsableStance(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    ) =>
        affordableAbilities
            .OfType<BuffAbility>()
            .FirstOrDefault(a => a.Duration <= 1 && IsUnused(enemy, a));

    private static bool IsUnused(Combatant enemy, Ability ability) =>
        enemy.ActiveBuffs.All(b => b.AbilityName != ability.Name);

    private static bool IsLow(int current, int maximum, float threshold) =>
        maximum > 0 && current / (float)maximum < threshold;

    private static ConsumableItemSnapshot? FindPotion(Combatant enemy, ResourceType resource) =>
        enemy.ConsumableItemSnapshots.FirstOrDefault(i => i.Resource == resource && i.Quantity > 0);

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

        var combatEvents = hotEvents.Concat(dotEvents).ToList();

        if (player.IsAlive)
        {
            var enemyTurnOrder = enemies
                .Where(e => e.IsAlive)
                .OrderByDescending(e => e.CalculateEffectiveAttribute(AttributeName.Dexterity));

            foreach (var enemy in enemyTurnOrder)
            {
                combatEvents.AddRange(ProcessTurn(enemy, ResolveEnemyAction(enemy, player)));
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
