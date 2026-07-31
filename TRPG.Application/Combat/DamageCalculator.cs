using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class DamageCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public int CalculateDamage(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon = null
    )
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
                    weapon ?? attacker.MainHandWeapon,
                    ability.DamageAmount,
                    ability.DamageAmountType,
                    (min, max) => Random.Shared.Next(min, max + 1)
                )
                : CalculateMagicRawDamage(attacker, ability);

        var withBonus = ApplyCreatureTypeBonus(rawDamage, ability, defender);
        return CalculateDamage(ApplyCrit(withBonus, attacker), ability.DamageType, defender);
    }

    public int EstimateDamage(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon = null
    )
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
                    weapon ?? attacker.MainHandWeapon,
                    ability.DamageAmount,
                    ability.DamageAmountType,
                    (min, max) => (min + max) / 2
                )
                : CalculateMagicRawDamage(attacker, ability);

        var withBonus = ApplyCreatureTypeBonus(rawDamage, ability, defender);
        return CalculateDamage(
            ApplyExpectedCrit(withBonus, attacker),
            ability.DamageType,
            defender
        );
    }

    public int CalculateDamage(float amount, DamageType type, Combatant defender)
    {
        var resistance = Math.Min(
            optionsSnapshot.Value.MaxResistancePercent,
            defender.ResistanceFor(type)
        );
        var mitigated = amount * (1 - resistance);

        return Math.Max(0, (int)mitigated);
    }

    private float CalculatePhysicalRawDamage(
        Combatant attacker,
        Weapon? weapon,
        float damageAmount,
        AmountType damageAmountType,
        Func<int, int, int> rollRange
    )
    {
        var strengthBonus = attacker.Strength * optionsSnapshot.Value.StrengthDamageBonusPerPoint;

        var effectiveWeapon = attacker.ActiveConditions[ConditionType.Disarmed] > 0 ? null : weapon;
        var roll = effectiveWeapon is null
            ? rollRange(attacker.NaturalWeaponMinDamage, attacker.NaturalWeaponMaxDamage)
            : rollRange(effectiveWeapon.MinDamage, effectiveWeapon.MaxDamage);

        var baseDamage =
            damageAmountType == AmountType.Percent
                ? roll * (damageAmount / 100f)
                : roll + damageAmount;

        return baseDamage * (1 + strengthBonus);
    }

    private float CalculateMagicRawDamage(Combatant attacker, AttackAbility ability)
    {
        var intelligence = attacker.Intelligence;
        var intelligenceBonus = MathF.Log(
            1 + intelligence / optionsSnapshot.Value.IntelligenceDamageLogDivisor
        );

        return ability.DamageAmount * (1 + intelligenceBonus);
    }

    private static float ApplyCreatureTypeBonus(
        float rawDamage,
        AttackAbility ability,
        Combatant defender
    ) =>
        ability.BonusTargetCreatureType == defender.CreatureType
            ? rawDamage * ability.BonusDamageMultiplier
            : rawDamage;

    private static float ApplyCrit(float rawDamage, Combatant attacker) =>
        Random.Shared.NextDouble() < attacker.CritChance
            ? rawDamage * attacker.CritDamageMultiplier
            : rawDamage;

    private static float ApplyExpectedCrit(float rawDamage, Combatant attacker) =>
        rawDamage * (1 + attacker.CritChance * (attacker.CritDamageMultiplier - 1));
}
