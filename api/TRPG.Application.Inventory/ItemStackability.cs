using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

public static class ItemStackability
{
    public static bool IsStackable(Item item) =>
        item switch
        {
            Consumable or Ammunition or Gold => true,
            Weapon weapon => weapon.Type == WeaponType.Javelin,
            _ => false,
        };
}
