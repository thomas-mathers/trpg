using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal static class ItemStackability
{
    public static bool IsStackable(Item item) =>
        item switch
        {
            Consumable or Ammunition or Gold => true,
            Weapon weapon => weapon.Type == WeaponType.Javelin,
            _ => false,
        };
}
