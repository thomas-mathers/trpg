using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

public class HitCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public bool RollHit(
        Combatant attacker,
        AttackAbility ability,
        Combatant defender,
        Weapon? weapon = null
    ) => ability.DamageType != DamageType.Physical || RollWeaponHit(attacker, defender, weapon);

    public bool RollBlock(AttackAbility ability, Combatant defender) =>
        ability.DamageType == DamageType.Physical && RollWeaponBlock(defender);

    private bool RollWeaponHit(Combatant attacker, Combatant defender, Weapon? weapon)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = CalculateHitChance(attacker, defender, weapon);

        return hitRoll < hitChance;
    }

    private bool RollWeaponBlock(Combatant defender)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = defender.BlockChance;

        return hitRoll < hitChance;
    }

    public float CalculateHitChance(Combatant attacker, Combatant defender, Weapon? weapon = null)
    {
        var settings = optionsSnapshot.Value;
        var defense = defender.Defense;
        var evasion = defender.Evasion;
        var attackRating = attacker.AttackRatingFor(weapon ?? attacker.MainHandWeapon);

        if (attackRating + defense + evasion == 0)
        {
            return settings.MinHitChance;
        }

        return Math.Clamp(
            attackRating / (attackRating + defense + evasion),
            settings.MinHitChance,
            settings.MaxHitChance
        );
    }
}
