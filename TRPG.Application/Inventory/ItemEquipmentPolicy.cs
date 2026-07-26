using System.Text.Json;
using System.Text.Json.Nodes;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal static class ItemEquipmentPolicy
{
    public static EquipmentSlot? GetDefaultSlot(Item item) =>
        item switch
        {
            Weapon => EquipmentSlot.RightHand,
            Shield => EquipmentSlot.LeftHand,
            Ammunition => EquipmentSlot.LeftHand,
            Armor armor => armor.Type switch
            {
                ArmorType.Helm => EquipmentSlot.Helm,
                ArmorType.Chest => EquipmentSlot.Chest,
                ArmorType.Boots => EquipmentSlot.Boots,
                ArmorType.Gloves => EquipmentSlot.Gloves,
                _ => null,
            },
            Accessory accessory => accessory.Type switch
            {
                AccessoryType.Necklace => EquipmentSlot.Necklace,
                AccessoryType.Belt => EquipmentSlot.Belt,
                AccessoryType.Ring => EquipmentSlot.LeftRing,
                _ => null,
            },
            _ => null,
        };

    public static bool IsStackable(Item item) =>
        item switch
        {
            Consumable or Ammunition or Gold => true,
            Weapon weapon => weapon.Type == WeaponType.Javelin,
            _ => false,
        };

    public static Item Split(Item item, int quantity, Guid ownerId, OwnerType ownerType)
    {
        var type = item.GetType();
        var node = JsonSerializer.SerializeToNode(item, type)!.AsObject();
        node["Id"] = Guid.NewGuid();
        node["Quantity"] = quantity;
        node["Ownership"] = JsonSerializer.SerializeToNode(
            new ItemOwnership { OwnerId = ownerId, OwnerType = ownerType }
        );
        return (Item)node.Deserialize(type)!;
    }
}
