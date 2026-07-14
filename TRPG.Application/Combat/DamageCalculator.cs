using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class DamageCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public int CalculateDamage(Combatant attacker, AttackAbility ability, Combatant defender)
    {
        var rawDamage =
            ability.DamageType == DamageType.Physical
                ? CalculatePhysicalRawDamage(attacker, ability)
                : CalculateMagicRawDamage(attacker, ability);

        return CalculateDamage(rawDamage, ability.DamageType, defender);
    }

    public int CalculateDamage(float amount, DamageType type, Combatant defender)
    {
        var resistance = defender.CalculateEffectiveAttribute(ResistanceAttributeFor(type));
        var mitigated = amount * (1 - resistance);

        return Math.Max(0, (int)mitigated);
    }

    private float CalculatePhysicalRawDamage(Combatant attacker, AttackAbility ability)
    {
        var strengthBonus =
            attacker.CalculateEffectiveAttribute(AttributeName.Strength)
            * optionsSnapshot.Value.StrengthDamageBonusPerPoint;

        var weapon = attacker.Weapon;
        var weaponRoll = weapon is null
            ? optionsSnapshot.Value.UnarmedBaseDamage
            : Random.Shared.Next(weapon.MinDamage, weapon.MaxDamage + 1);

        return weaponRoll * (ability.DamageAmount / 100f) * (1 + strengthBonus);
    }

    private float CalculateMagicRawDamage(Combatant attacker, AttackAbility ability)
    {
        var intelligenceBonus =
            attacker.CalculateEffectiveAttribute(AttributeName.Intelligence)
            * optionsSnapshot.Value.IntelligenceDamageBonusPerPoint;

        return ability.DamageAmount * (1 + intelligenceBonus);
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
