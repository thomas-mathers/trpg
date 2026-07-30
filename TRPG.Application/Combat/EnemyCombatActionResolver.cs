using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using ActiveBuff = TRPG.Application.Creatures.ActiveBuff;

namespace TRPG.Application.Combat;

public class EnemyCombatActionResolver(
    IOptionsSnapshot<CombatOptions> optionsSnapshot,
    DamageCalculator damageCalculator,
    HitCalculator hitCalculator
)
{
    internal ResolvedCombatAction Resolve(Combatant enemy, Combatant player)
    {
        var affordableAbilities = enemy
            .Abilities.Where(a =>
                enemy.CooldownRemainingByAbility[a.Name] == 0
                && enemy.CurrentAp >= a.ApCost
                && enemy.CurrentMp >= a.MpCost
                && AbilityGearRequirement.IsMet(enemy, a)
            )
            .ToArray();

        var resourceAction = ResolveResourceAction(enemy, affordableAbilities);
        if (resourceAction is not null)
        {
            return resourceAction;
        }

        // Purely a tactical/flavor choice, unlike the resource logic below, so it's the one
        // decision allowed to be a coin flip rather than always taking the "best" option.
        var openingBuff = FindUsableOpeningBuff(enemy, player, affordableAbilities);
        if (
            openingBuff is not null
            && Random.Shared.NextDouble() < optionsSnapshot.Value.OpeningBuffChancePercent
        )
        {
            return new ResolvedUseAbilityAction(openingBuff, [enemy]);
        }

        // Every combatant always has the basic attack (0 AP/MP cost, 0 cooldown), so it's always
        // in affordableAbilities and MaxBy can never return null here. Ranked by actual expected
        // damage against this target (weapon roll, strength/intelligence bonuses, the target's
        // real resistances, and hit chance all included) rather than raw ability metadata -
        // comparing DamageAmount directly made a small flat-damage ability (a non-physical
        // ability like a poison arrow) look bigger than a percent-of-weapon physical attack just
        // because its number happened to be larger, even though it deals far less real damage.
        // Factoring in hit chance matters just as much: a hard-hitting physical attack that only
        // connects 1-in-6 times is worse in practice than a reliable spell that always lands.
        var bestAttackAbility = affordableAbilities
            .OfType<AttackAbility>()
            .MaxBy(a => EstimateExpectedDamage(enemy, a, player));

        return new ResolvedUseAbilityAction(bestAttackAbility!, [player]);
    }

    // Magic attacks always hit (see HitCalculator.DidHit), so only physical attacks discount
    // their damage-if-it-hits by the actual chance of it hitting at all.
    private float EstimateExpectedDamage(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender
    )
    {
        var damage = damageCalculator.EstimateDamage(attacker, ability, defender);

        return ability.DamageType == DamageType.Physical
            ? damage * hitCalculator.CalculateHitChance(attacker, defender)
            : damage;
    }

    // A monster prioritizes surviving over anything else, deterministically — always the best
    // available option, never left to chance. A heal ability beats a health potion (no
    // inventory cost); a single-turn stance like Block is only used as a last resort once
    // there's nothing left to heal with, rather than eating a hit unguarded — never as an
    // opening move before any damage was taken.
    private ResolvedCombatAction? ResolveResourceAction(
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
        return mpPotion is not null ? new ResolvedUseItemAction(mpPotion) : null;
    }

    // A real skill-tree buff meant to be cast once and left active for several rounds. Ranked by
    // combined value: how much more damage the monster's own best attack would deal with the
    // buff active, plus how much less damage the player's best attack against this monster would
    // deal - both measured with the same EstimateDamage used for attack selection, so a Strength
    // buff scores well for the same reason it'd help an actual attack, and a resistance buff
    // scores well for blunting the player's biggest threat. No randomness in which buff wins;
    // OpeningBuffChancePercent above is the only randomized part of this decision.
    private Ability? FindUsableOpeningBuff(
        Combatant enemy,
        Combatant player,
        IReadOnlyList<Ability> affordableAbilities
    )
    {
        var candidates = affordableAbilities
            .OfType<BuffAbility>()
            .Where(a => a.Duration > 1 && IsUnused(enemy, a))
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        var enemyBestAttack = enemy
            .Abilities.OfType<AttackAbility>()
            .MaxBy(a => EstimateExpectedDamage(enemy, a, player));
        var playerBestAttack = player
            .Abilities.OfType<AttackAbility>()
            .MaxBy(a => EstimateExpectedDamage(player, a, enemy));

        var baselineOffense = enemyBestAttack is null
            ? 0
            : EstimateExpectedDamage(enemy, enemyBestAttack, player);
        var baselineDefense = playerBestAttack is null
            ? 0
            : EstimateExpectedDamage(player, playerBestAttack, enemy);

        return candidates.MaxBy(buff =>
            ScoreBuff(
                enemy,
                player,
                buff,
                enemyBestAttack,
                playerBestAttack,
                baselineOffense,
                baselineDefense
            )
        );
    }

    private float ScoreBuff(
        Combatant enemy,
        Combatant player,
        BuffAbility buff,
        AttackAbility? enemyBestAttack,
        AttackAbility? playerBestAttack,
        float baselineOffense,
        float baselineDefense
    )
    {
        ApplyTemporaryModifiers(enemy, buff);

        var buffedOffense = enemyBestAttack is null
            ? 0
            : EstimateExpectedDamage(enemy, enemyBestAttack, player);
        var buffedDefense = playerBestAttack is null
            ? 0
            : EstimateExpectedDamage(player, playerBestAttack, enemy);

        RemoveTemporaryModifiers(enemy, buff);

        var offensiveGain = buffedOffense - baselineOffense;
        var defensiveGain = baselineDefense - buffedDefense;

        return offensiveGain + defensiveGain;
    }

    // Mirrors CombatEngine.ApplyBuff's real modifier-to-ActiveBuff conversion, applied
    // temporarily just to measure this candidate's value - always paired with
    // RemoveTemporaryModifiers so the monster's real state is untouched once scoring is done.
    private static void ApplyTemporaryModifiers(Combatant enemy, BuffAbility buff)
    {
        var modifiers =
            buff.ParryCapableModifiers.Count > 0 && AbilityGearRequirement.IsParryCapable(enemy)
                ? buff.ParryCapableModifiers
                : buff.Modifiers;

        foreach (var modifier in modifiers)
        {
            enemy.ActiveBuffs.Add(
                new ActiveBuff
                {
                    AbilityName = buff.Name,
                    Amount = modifier.Amount,
                    AmountType = modifier.AmountType,
                    Attribute = modifier.Attribute,
                    RemainingTurns = buff.Duration,
                }
            );
        }
    }

    private static void RemoveTemporaryModifiers(Combatant enemy, BuffAbility buff) =>
        enemy.ActiveBuffs.RemoveAll(b => b.AbilityName == buff.Name);

    // A single-turn stance (Duration 1, like Block) is meant to be triggered situationally
    // rather than left running, unlike a real skill-tree buff — never an interchangeable
    // candidate for the opening-move decision above.
    private static Ability? FindUsableStance(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    ) =>
        PickRandom(
            affordableAbilities
                .OfType<BuffAbility>()
                .Where(a => a.Duration <= 1 && IsUnused(enemy, a))
        );

    private static Ability? PickRandom(IEnumerable<Ability> candidates)
    {
        var pool = candidates.ToArray();
        return pool.Length > 0 ? pool[Random.Shared.Next(pool.Length)] : null;
    }

    private static bool IsUnused(Combatant enemy, Ability ability) =>
        enemy.ActiveBuffs.All(b => b.AbilityName != ability.Name);

    private static bool IsLow(int current, int maximum, float threshold) =>
        maximum > 0 && current / (float)maximum < threshold;

    private static ConsumableItemSnapshot? FindPotion(Combatant enemy, ResourceType resource) =>
        enemy.ConsumableItemSnapshots.FirstOrDefault(i => i.Resource == resource && i.Quantity > 0);
}
