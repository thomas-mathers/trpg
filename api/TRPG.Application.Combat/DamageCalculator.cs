using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

public record DamageResult(int Amount, bool IsCritical);

public class DamageCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public DamageResult CalculateDamage(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon = null,
        bool isSneakAttack = false
    )
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
                    weapon ?? attacker.MainHandWeapon,
                    ability.DamageAmount,
                    ability.DamageAmountType,
                    (minimum, maximum) => Random.Shared.Next(minimum, maximum + 1)
                )
                : CalculateMagicRawDamage(attacker, ability);

        var withBonus = ApplyCreatureTypeBonus(rawDamage, ability, defender);
        var withSneakBonus =
            isSneakAttack && ability.DamageType == DamageType.Physical
                ? withBonus * optionsSnapshot.Value.SneakAttackDamageMultiplier
                : withBonus;
        var isCritical = Random.Shared.NextDouble() < attacker.CritChance;
        var critedDamage = isCritical
            ? withSneakBonus * attacker.CritDamageMultiplier
            : withSneakBonus;
        var elementalDamage = CalculateElementalDamage(attacker, ability, weapon, defender);
        return new DamageResult(
            CalculateDamage(critedDamage, ability.DamageType, defender) + elementalDamage,
            isCritical
        );
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
                    (minimum, maximum) => (minimum + maximum) / 2
                )
                : CalculateMagicRawDamage(attacker, ability);

        var withBonus = ApplyCreatureTypeBonus(rawDamage, ability, defender);
        var elementalDamage = EstimateElementalDamage(attacker, ability, weapon, defender);
        return CalculateDamage(ApplyExpectedCrit(withBonus, attacker), ability.DamageType, defender)
            + (int)elementalDamage;
    }

    public float EstimateRawDamage(Combatant attacker, AttackAbility ability, Weapon? weapon = null)
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
                    weapon ?? attacker.MainHandWeapon,
                    ability.DamageAmount,
                    ability.DamageAmountType,
                    (minimum, maximum) => (minimum + maximum) / 2
                )
                : CalculateMagicRawDamage(attacker, ability);

        return ApplyExpectedCrit(rawDamage, attacker)
            + EstimateElementalDamage(attacker, ability, weapon);
    }

    public float EstimateBasicAttackDamagePerTurn(Combatant attacker)
    {
        var mainHandWeapon = attacker.MainHandWeapon;
        var offHandWeapon = attacker.OffHandWeapon;
        var mainHandSwings = 1 + Math.Max(0, (mainHandWeapon?.AttacksPerTurn ?? 1) - 1);
        var offHandSwings = offHandWeapon?.AttacksPerTurn ?? 0;

        return mainHandSwings * EstimateRawDamage(attacker, AbilityCatalog.Strike, mainHandWeapon)
            + offHandSwings * EstimateRawDamage(attacker, AbilityCatalog.Strike, offHandWeapon);
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

    private int CalculateElementalDamage(
        Combatant attacker,
        AttackAbility ability,
        Weapon? weapon,
        Combatant defender
    ) =>
        GetElementalDamageModifiers(attacker, ability, weapon)
            .Sum(modifier =>
                CalculateDamage(
                    Random.Shared.Next(modifier.MinDamage, modifier.MaxDamage + 1),
                    modifier.DamageType,
                    defender
                )
            );

    private float EstimateElementalDamage(
        Combatant attacker,
        AttackAbility ability,
        Weapon? weapon,
        Combatant? defender = null
    ) =>
        GetElementalDamageModifiers(attacker, ability, weapon)
            .Sum(modifier =>
            {
                var averageDamage = (modifier.MinDamage + modifier.MaxDamage) / 2f;
                return defender is null
                    ? averageDamage
                    : CalculateDamage(averageDamage, modifier.DamageType, defender);
            });

    private static IReadOnlyCollection<ElementalDamageModifier> GetElementalDamageModifiers(
        Combatant attacker,
        AttackAbility ability,
        Weapon? weapon
    )
    {
        var swungWeapon = weapon ?? attacker.MainHandWeapon;

        return
            ability.DamageType == DamageType.Physical
            && attacker.ActiveConditions[ConditionType.Disarmed] == 0
            && swungWeapon is not null
            ? swungWeapon.Modifiers.OfType<ElementalDamageModifier>().ToArray()
            : [];
    }

    private static float ApplyCreatureTypeBonus(
        float rawDamage,
        AttackAbility ability,
        Combatant defender
    ) =>
        ability.BonusTargetCreatureType == defender.CreatureType
            ? rawDamage * ability.BonusDamageMultiplier
            : rawDamage;

    private static float ApplyExpectedCrit(float rawDamage, Combatant attacker) =>
        rawDamage * (1 + attacker.CritChance * (attacker.CritDamageMultiplier - 1));
}
