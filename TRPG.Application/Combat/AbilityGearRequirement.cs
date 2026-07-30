using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public static class AbilityGearRequirement
{
    public static readonly IReadOnlyDictionary<WeaponType, Skill> WeaponSkills = new Dictionary<
        WeaponType,
        Skill
    >
    {
        [WeaponType.Sword] = Skill.Melee,
        [WeaponType.Dagger] = Skill.Melee,
        [WeaponType.Axe] = Skill.Melee,
        [WeaponType.Mace] = Skill.Melee,
        [WeaponType.Hammer] = Skill.Melee,
        [WeaponType.Bow] = Skill.Archery,
        [WeaponType.Crossbow] = Skill.Archery,
        [WeaponType.Javelin] = Skill.Archery,
        [WeaponType.Staff] = Skill.Destruction,
        [WeaponType.Wand] = Skill.Destruction,
    };

    // Shield or melee weapon: something you can actively deflect a blow with, as opposed to
    // bracing bare-handed or with a ranged/casting weapon that isn't built for it.
    public static bool IsParryCapable(Combatant actor) =>
        actor.Shield != null
        || (
            actor.Weapon is { } weapon && WeaponSkills.GetValueOrDefault(weapon.Type) == Skill.Melee
        );

    // Per-ability, not per-skill: most Blocking-tree abilities (e.g. Block itself) need no
    // particular gear, so this isn't derived from Skill the way WeaponSkills is.
    private static readonly HashSet<string> AbilitiesRequiringShield = ["Shield Bash"];

    // Destruction/Illusion are deliberately excluded — casting works without a staff/wand equipped.
    private static readonly HashSet<Skill> WeaponRequiredSkills = [Skill.Melee, Skill.Archery];

    public static bool IsMet(Combatant actor, Ability ability)
    {
        if (AbilitiesRequiringShield.Contains(ability.Name))
        {
            return actor.Shield != null;
        }

        if (!WeaponRequiredSkills.Contains(ability.Skill))
        {
            return true;
        }

        return actor.Weapon is { } weapon
            && WeaponSkills.GetValueOrDefault(weapon.Type) == ability.Skill;
    }

    public static string DescribeRequirement(Ability ability)
    {
        if (AbilitiesRequiringShield.Contains(ability.Name))
        {
            return "a shield";
        }

        return ability.Skill switch
        {
            Skill.Melee => "a melee weapon",
            Skill.Archery => "a bow",
            _ => "the right gear",
        };
    }
}
