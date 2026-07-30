using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public class HitCalculator(IOptionsSnapshot<CombatOptions> optionsSnapshot)
{
    public bool DidHit(Combatant attacker, AttackAbility ability, Combatant defender) =>
        ability.DamageType != DamageType.Physical || DidHitWithWeapon(attacker, defender);

    public bool DidBlock(AttackAbility ability, Combatant defender) =>
        ability.DamageType == DamageType.Physical && DidBlockWeaponSwing(defender);

    // A bonus swing from a fast weapon (see WeaponAttackSpeed) is always resolved as a plain
    // physical strike, regardless of the ability that triggered it - DidHit/DidBlock gate on the
    // ability's own damage type, which is wrong for a swing that isn't carrying that ability at all.
    public bool DidHitWithWeapon(Combatant attacker, Combatant defender)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = CalculateHitChance(attacker, defender);

        return hitRoll < hitChance;
    }

    public bool DidBlockWeaponSwing(Combatant defender)
    {
        var hitRoll = Random.Shared.NextSingle();
        var hitChance = defender.BlockChance;

        return hitRoll < hitChance;
    }

    public float CalculateHitChance(Combatant attacker, Combatant defender)
    {
        var settings = optionsSnapshot.Value;
        var defense = defender.CalculateEffectiveAttribute(AttributeName.Defense);
        var evasion =
            CalculateSnaredDexterity(defender, settings.SnareDexterityReductionPercent)
            * settings.EvasionPerDexterityPoint;

        // The player's proficiency is driven by actual accumulated practice (see
        // AdjustWeaponProficienciesCommandHandler); every non-player combatant uses a purely
        // formulaic stand-in instead, regardless of whether it's swinging a real weapon or
        // fighting unarmed - see CombatOptions.NonPlayerProficiencyBase/PerLevel.
        var proficiency = attacker.IsPlayer
            ? settings.BaseProficiency + (attacker.WeaponProficiency ?? 0)
            : settings.NonPlayerProficiencyBase
                + settings.NonPlayerProficiencyPerLevel * attacker.Level;

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

    // Snared has no dedicated incapacitation check (unlike Frozen/Stunned/Blinded/Silenced - see
    // CombatEngine.GetIncapacitationEvent); instead it hobbles effective Dexterity wherever it's
    // read for combat purposes.
    private static float CalculateSnaredDexterity(
        Combatant combatant,
        float snareDexterityReductionPercent
    )
    {
        var dexterity = combatant.CalculateEffectiveAttribute(AttributeName.Dexterity);
        return combatant.ActiveConditions[ConditionType.Snared] > 0
            ? dexterity * (1 - snareDexterityReductionPercent)
            : dexterity;
    }
}
