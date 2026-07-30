using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

public static class AbilityGearRequirement
{
    // Which skill trains when this weapon type lands a swing (see CombatEngine.GetTrainedSkill) -
    // a separate concern from which GearRequirement category the weapon satisfies below.
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

    private static readonly IReadOnlyDictionary<
        WeaponType,
        GearRequirement
    > WeaponGearRequirements = new Dictionary<WeaponType, GearRequirement>
    {
        [WeaponType.Sword] = GearRequirement.MeleeWeapon,
        [WeaponType.Dagger] = GearRequirement.MeleeWeapon,
        [WeaponType.Axe] = GearRequirement.MeleeWeapon,
        [WeaponType.Mace] = GearRequirement.MeleeWeapon,
        [WeaponType.Hammer] = GearRequirement.MeleeWeapon,
        [WeaponType.Bow] = GearRequirement.RangedWeapon,
        [WeaponType.Crossbow] = GearRequirement.RangedWeapon,
        [WeaponType.Javelin] = GearRequirement.RangedWeapon,
        [WeaponType.Staff] = GearRequirement.CasterWeapon,
        [WeaponType.Wand] = GearRequirement.CasterWeapon,
    };

    // Shield or melee weapon: something you can actively deflect a blow with, as opposed to
    // bracing bare-handed or with a ranged/casting weapon that isn't built for it.
    public static bool IsParryCapable(Combatant actor) =>
        actor.Shield != null || HasMeleeWeaponEquipped(actor);

    public static bool IsMet(Combatant actor, Ability ability) =>
        ability.GearRequirement switch
        {
            GearRequirement.Shield => actor.Shield != null,
            GearRequirement.None => true,
            _ => actor.Weapon is { } weapon
                && WeaponGearRequirements.GetValueOrDefault(weapon.Type) == ability.GearRequirement,
        };

    public static string DescribeRequirement(Ability ability) =>
        ability.GearRequirement switch
        {
            GearRequirement.Shield => "a shield",
            GearRequirement.MeleeWeapon => "a melee weapon",
            GearRequirement.RangedWeapon => "a bow",
            GearRequirement.CasterWeapon => "a staff or wand",
            _ => "the right gear",
        };

    private static bool HasMeleeWeaponEquipped(Combatant actor) =>
        actor.Weapon is { } weapon
        && WeaponGearRequirements.GetValueOrDefault(weapon.Type) == GearRequirement.MeleeWeapon;
}
