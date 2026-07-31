using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class HitCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public bool RollHit(Combatant attacker, AttackAbility ability, Combatant defender) =>
        ability.DamageType != DamageType.Physical || RollWeaponHit(attacker, defender);

    public bool RollBlock(AttackAbility ability, Combatant defender) =>
        ability.DamageType == DamageType.Physical && RollWeaponBlock(defender);

    private bool RollWeaponHit(Combatant attacker, Combatant defender)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = CalculateHitChance(attacker, defender);

        return hitRoll < hitChance;
    }

    private bool RollWeaponBlock(Combatant defender)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = defender.BlockChance;

        return hitRoll < hitChance;
    }

    public float CalculateHitChance(Combatant attacker, Combatant defender)
    {
        var settings = optionsSnapshot.Value;
        var defense = defender.Defense;
        var evasion = defender.Evasion;
        var proficiency = attacker.Proficiency;

        if (proficiency + defense + evasion == 0)
        {
            return settings.MinHitChance;
        }

        return Math.Clamp(
            proficiency / (proficiency + defense + evasion),
            settings.MinHitChance,
            settings.MaxHitChance
        );
    }
}
