using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class HitCalculator(CombatSettings combatSettings)
{
    public bool DidHit(Combatant attacker, AttackAbility ability, Combatant defender)
    {
        if (ability.DamageType != DamageType.Physical)
        {
            return true;
        }

        var hitRoll = Random.Shared.NextSingle();
        var hitChance = CalculateHitChance(attacker, defender);

        return hitRoll < hitChance;
    }

    public bool DidBlock(AttackAbility ability, Combatant defender)
    {
        if (ability.DamageType != DamageType.Physical)
        {
            return false;
        }

        var hitRoll = Random.Shared.NextSingle();
        var hitChance = defender.BlockChance;

        return hitRoll < hitChance;
    }

    public float CalculateHitChance(Combatant attacker, Combatant defender)
    {
        var defense = defender.CalculateEffectiveAttribute(AttributeName.Defense);

        var proficiency = combatSettings.BaseProficiency + (attacker.WeaponProficiency ?? 0);

        if (proficiency + defense == 0)
        {
            return combatSettings.MinHitChance;
        }

        return Math.Clamp(
            proficiency / (proficiency + defense),
            combatSettings.MinHitChance,
            combatSettings.MaxHitChance
        );
    }
}
