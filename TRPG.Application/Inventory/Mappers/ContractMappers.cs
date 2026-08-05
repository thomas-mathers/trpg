using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Mappers;

internal static class ContractMappers
{
    public static EquipmentSlot ToDataModel(
        this Contracts.Inventory.Responses.EquipmentSlot slot
    ) =>
        slot switch
        {
            Contracts.Inventory.Responses.EquipmentSlot.Helm => EquipmentSlot.Helm,
            Contracts.Inventory.Responses.EquipmentSlot.Chest => EquipmentSlot.Chest,
            Contracts.Inventory.Responses.EquipmentSlot.LeftHand => EquipmentSlot.LeftHand,
            Contracts.Inventory.Responses.EquipmentSlot.RightHand => EquipmentSlot.RightHand,
            Contracts.Inventory.Responses.EquipmentSlot.Boots => EquipmentSlot.Boots,
            Contracts.Inventory.Responses.EquipmentSlot.Necklace => EquipmentSlot.Necklace,
            Contracts.Inventory.Responses.EquipmentSlot.Gloves => EquipmentSlot.Gloves,
            Contracts.Inventory.Responses.EquipmentSlot.LeftRing => EquipmentSlot.LeftRing,
            Contracts.Inventory.Responses.EquipmentSlot.RightRing => EquipmentSlot.RightRing,
            Contracts.Inventory.Responses.EquipmentSlot.Belt => EquipmentSlot.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
}
