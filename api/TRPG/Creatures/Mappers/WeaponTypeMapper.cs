using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class WeaponTypeMapper
{
    public static ItemType ToResponse(this WeaponType type) =>
        type switch
        {
            WeaponType.Dagger => ItemType.Dagger,
            WeaponType.Sword => ItemType.Sword,
            WeaponType.Axe => ItemType.Axe,
            WeaponType.Mace => ItemType.Mace,
            WeaponType.Hammer => ItemType.Hammer,
            WeaponType.Staff => ItemType.Staff,
            WeaponType.Wand => ItemType.Wand,
            WeaponType.Bow => ItemType.Bow,
            WeaponType.Crossbow => ItemType.Crossbow,
            WeaponType.Javelin => ItemType.Javelin,
            WeaponType.GreatSword => ItemType.GreatSword,
            WeaponType.GreatAxe => ItemType.GreatAxe,
            WeaponType.GreatHammer => ItemType.GreatHammer,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
