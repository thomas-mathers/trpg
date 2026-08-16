using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

public static class EquipmentLoadoutPolicy
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

    public static IReadOnlyCollection<EquipmentSlot> GetFootprint(Item item, EquipmentSlot slot) =>
        item is Weapon { IsTwoHanded: true }
            ? [EquipmentSlot.RightHand, EquipmentSlot.LeftHand]
            : [slot];

    public static EquipmentSlot ResolveEquippedSlot(Item item, EquipmentSlot requestedSlot) =>
        item is Weapon { IsTwoHanded: true } ? EquipmentSlot.RightHand : requestedSlot;

    public static IReadOnlyCollection<Item> GetConflictingItems(
        Item toEquip,
        EquipmentSlot slot,
        IReadOnlyCollection<Item> currentlyEquippedItems
    )
    {
        var newFootprint = GetFootprint(toEquip, slot);

        return currentlyEquippedItems
            .Where(i => i.Id != toEquip.Id)
            .Where(i =>
                GetFootprint(i, i.Ownership.EquippedSlot!.Value).Intersect(newFootprint).Any()
            )
            .ToArray();
    }
}
