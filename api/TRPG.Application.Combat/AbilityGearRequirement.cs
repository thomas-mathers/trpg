using TRPG.Application.Abilities;
using TRPG.Data.Models;

namespace TRPG.Application.Combat;

internal static class AbilityGearRequirement
{
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
        [WeaponType.GreatSword] = GearRequirement.MeleeWeapon,
        [WeaponType.GreatAxe] = GearRequirement.MeleeWeapon,
        [WeaponType.GreatHammer] = GearRequirement.MeleeWeapon,
        [WeaponType.Bow] = GearRequirement.RangedWeapon,
        [WeaponType.Crossbow] = GearRequirement.RangedWeapon,
        [WeaponType.Javelin] = GearRequirement.RangedWeapon,
        [WeaponType.Staff] = GearRequirement.CasterWeapon,
        [WeaponType.Wand] = GearRequirement.CasterWeapon,
    };

    public static bool IsParryCapable(Combatant actor) =>
        actor.Shield != null || HasMeleeWeaponEquipped(actor);

    public static bool IsMet(Combatant actor, Ability ability) =>
        ability.GearRequirement switch
        {
            GearRequirement.Shield => actor.Shield != null,
            GearRequirement.None => true,
            _ => actor.MainHandWeapon is { } weapon
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
        actor.MainHandWeapon is { } weapon
        && WeaponGearRequirements.GetValueOrDefault(weapon.Type) == GearRequirement.MeleeWeapon;
}
