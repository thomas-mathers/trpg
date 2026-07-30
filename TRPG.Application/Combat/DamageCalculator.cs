using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class DamageCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public int CalculateDamage(Combatant attacker, AttackAbility ability, Combatant defender)
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
                    ability.DamageAmount,
                    ability.DamageAmountType,
                    (min, max) => Random.Shared.Next(min, max + 1)
                )
                : CalculateMagicRawDamage(attacker, ability);

        var withBonus = ApplyCreatureTypeBonus(rawDamage, ability, defender);
        return CalculateDamage(ApplyCrit(withBonus, attacker), ability.DamageType, defender);
    }

    // Same formula as CalculateDamage, but with a deterministic (average, not rolled) weapon
    // damage, so callers ranking abilities against each other (e.g. enemy AI choosing its best
    // attack) get a reproducible comparison instead of re-rolling a different answer every time
    // they ask. Real damage application always goes through CalculateDamage instead.
    public int EstimateDamage(Combatant attacker, AttackAbility ability, Combatant defender)
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(
                    attacker,
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

    // A bonus swing from a fast weapon (see WeaponAttackSpeed) doesn't carry the triggering
    // ability's payload - it's the weapon striking again on its own, at a plain 100% weapon roll,
    // always physical regardless of what ability was actually used.
    public int CalculateBonusSwingDamage(Combatant attacker, Combatant defender)
    {
        var rawDamage = CalculatePhysicalRawDamage(
            attacker,
            damageAmount: 100f,
            AmountType.Percent,
            (min, max) => Random.Shared.Next(min, max + 1)
        );

        return CalculateDamage(ApplyCrit(rawDamage, attacker), DamageType.Physical, defender);
    }

    public int CalculateDamage(float amount, DamageType type, Combatant defender)
    {
        var resistance = Math.Min(
            optionsSnapshot.Value.MaxResistancePercent,
            defender.CalculateEffectiveAttribute(ResistanceAttributeFor(type))
        );
        var mitigated = amount * (1 - resistance);

        return Math.Max(0, (int)mitigated);
    }

    private float CalculatePhysicalRawDamage(
        Combatant attacker,
        float damageAmount,
        AmountType damageAmountType,
        Func<int, int, int> rollRange
    )
    {
        var strengthBonus =
            attacker.CalculateEffectiveAttribute(AttributeName.Strength)
            * optionsSnapshot.Value.StrengthDamageBonusPerPoint;

        // No equipped weapon: roll against this creature's own natural-weapon range instead (see
        // CreatureArchetype.NaturalWeaponDamage) - scaled by level the same way a real weapon is,
        // rather than a single flat constant shared by every unarmed creature in the game.
        // Disarmed forces this same fallback regardless of what's actually equipped - already
        // unarmed creatures (Beast, Elemental, Wraith) are naturally immune to it.
        var weapon = attacker.ActiveConditions[ConditionType.Disarmed] > 0 ? null : attacker.Weapon;
        var roll = weapon is null
            ? rollRange(attacker.NaturalWeaponMinDamage, attacker.NaturalWeaponMaxDamage)
            : rollRange(weapon.MinDamage, weapon.MaxDamage);

        // Percent-type abilities (a weapon/natural-weapon swing) scale off the roll; Flat-type
        // abilities add their own damage on top of it instead, so an unarmed attacker's Strength
        // bonus multiplies a real base rather than a token amount alone.
        var baseDamage =
            damageAmountType == AmountType.Percent
                ? roll * (damageAmount / 100f)
                : roll + damageAmount;

        return baseDamage * (1 + strengthBonus);
    }

    private float CalculateMagicRawDamage(Combatant attacker, AttackAbility ability)
    {
        var intelligence = attacker.CalculateEffectiveAttribute(AttributeName.Intelligence);
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

    // A second, universal payoff for Dexterity beyond evasion/turn order (see HitCalculator and
    // Combatant.TurnOrderValue) - applies to every damage type rather than gating on Physical, so
    // a finesse-built attacker's crit chance isn't a special case tied to weapon type.
    private float CalculateCritChance(Combatant attacker) =>
        Math.Min(
            optionsSnapshot.Value.MaxCritChance,
            attacker.CalculateCombatDexterity(optionsSnapshot.Value.SnareDexterityReductionPercent)
                * optionsSnapshot.Value.CritChancePerDexterityPoint
        );

    private float ApplyCrit(float rawDamage, Combatant attacker) =>
        Random.Shared.NextDouble() < CalculateCritChance(attacker)
            ? rawDamage * optionsSnapshot.Value.CritDamageMultiplier
            : rawDamage;

    // EstimateDamage's counterpart to ApplyCrit - an expected-value blend instead of a coin flip,
    // matching how EstimateDamage already averages a rolled weapon range instead of rolling it.
    private float ApplyExpectedCrit(float rawDamage, Combatant attacker)
    {
        var critChance = CalculateCritChance(attacker);
        return rawDamage * (1 + critChance * (optionsSnapshot.Value.CritDamageMultiplier - 1));
    }

    private static AttributeName ResistanceAttributeFor(DamageType damageType) =>
        damageType switch
        {
            DamageType.Physical => AttributeName.PhysicalResistance,
            DamageType.Fire => AttributeName.FireResistance,
            DamageType.Ice => AttributeName.IceResistance,
            DamageType.Lightning => AttributeName.LightningResistance,
            DamageType.Poison => AttributeName.PoisonResistance,
            DamageType.Magic => AttributeName.MagicResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(damageType)),
        };
}
