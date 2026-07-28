using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class EnemyCombatActionResolver(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    internal ResolvedCombatAction Resolve(Combatant enemy, Combatant player)
    {
        var affordableAbilities = enemy
            .Abilities.Where(a =>
                enemy.CooldownRemainingByAbility[a.Name] == 0
                && enemy.CurrentAp >= a.ApCost
                && enemy.CurrentMp >= a.MpCost
            )
            .ToArray();

        var defensiveAction = ResolveDefensiveAction(enemy, affordableAbilities);
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
    private ResolvedCombatAction? ResolveDefensiveAction(
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
}
