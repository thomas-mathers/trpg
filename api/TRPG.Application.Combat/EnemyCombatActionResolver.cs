using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;

namespace TRPG.Application.Combat;

internal class EnemyCombatActionResolver(
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

        var openingBuff = FindUsableOpeningBuff(enemy, player, affordableAbilities);
        if (
            openingBuff is not null
            && Random.Shared.NextDouble() < optionsSnapshot.Value.OpeningBuffChancePercent
        )
        {
            return new ResolvedUseAbilityAction(openingBuff, [enemy]);
        }

        var bestAttackAbility = affordableAbilities
            .OfType<AttackAbility>()
            .MaxBy(a => EstimateExpectedDamage(enemy, a, player));

        return new ResolvedUseAbilityAction(bestAttackAbility!, [player]);
    }

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

    private ResolvedCombatAction? ResolveResourceAction(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    )
    {
        var threshold = optionsSnapshot.Value.LowResourceThresholdPercent;

        if (IsLow(enemy.CurrentHp, enemy.MaximumHp, threshold))
        {
            var healAbility = affordableAbilities.FirstOrDefault(IsHealingAbility);
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

    private Ability? FindUsableOpeningBuff(
        Combatant enemy,
        Combatant player,
        IReadOnlyList<Ability> affordableAbilities
    )
    {
        var candidates = affordableAbilities
            .OfType<SupportAbility>()
            .Where(a => HasBuffs(a) && GetBuffDuration(a, enemy) > 1 && IsUnused(enemy, a))
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
        SupportAbility buff,
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

    private static void ApplyTemporaryModifiers(Combatant enemy, SupportAbility buff)
    {
        foreach (var modifier in GetBuffs(buff, enemy))
        {
            enemy.ActiveBuffs.Add(
                new ActiveBuff
                {
                    AbilityName = buff.Name,
                    Amount = modifier.Amount,
                    AmountType = modifier.AmountType,
                    Attribute = modifier.Attribute,
                    RemainingTurns = modifier.Duration,
                }
            );
        }
    }

    private static void RemoveTemporaryModifiers(Combatant enemy, SupportAbility buff) =>
        enemy.ActiveBuffs.RemoveAll(b => b.AbilityName == buff.Name);

    private static Ability? FindUsableStance(
        Combatant enemy,
        IReadOnlyList<Ability> affordableAbilities
    ) =>
        PickRandom(
            affordableAbilities
                .OfType<SupportAbility>()
                .Where(a => HasBuffs(a) && GetBuffDuration(a, enemy) <= 1 && IsUnused(enemy, a))
        );

    private static bool IsHealingAbility(Ability ability) =>
        ability is SupportAbility support && (support.HealAmount > 0 || support.Hots.Count > 0);

    private static bool HasBuffs(SupportAbility ability) =>
        ability.Buffs.Count > 0 || ability.BuffsWhileParrying.Count > 0;

    private static IReadOnlyList<AttributeEffect> GetBuffs(
        SupportAbility ability,
        Combatant enemy
    ) =>
        ability.BuffsWhileParrying.Count > 0 && AbilityGearRequirement.IsParryCapable(enemy)
            ? ability.BuffsWhileParrying
            : ability.Buffs;

    private static int GetBuffDuration(SupportAbility ability, Combatant enemy)
    {
        var buffs = GetBuffs(ability, enemy);
        return buffs.Count > 0 ? buffs[0].Duration : 0;
    }

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
