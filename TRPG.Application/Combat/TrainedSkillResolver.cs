using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public static class TrainedSkillResolver
{
    private static readonly IReadOnlyDictionary<WeaponType, Skill> WeaponSkills = new Dictionary<
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

    public static Skill Resolve(Combatant actor, Ability ability)
    {
        if (ability is not AttackAbility || ability.Skill != Skill.General)
        {
            return ability.Skill;
        }

        return actor.Weapon is { } weapon
            ? WeaponSkills.GetValueOrDefault(weapon.Type, Skill.General)
            : Skill.Unarmed;
    }
}
