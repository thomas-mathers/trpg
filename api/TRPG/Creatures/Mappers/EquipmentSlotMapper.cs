using ContractEquipmentSlot = TRPG.Contracts.Inventory.Responses.EquipmentSlot;
using DataEquipmentSlot = TRPG.Data.Models.EquipmentSlot;

namespace TRPG.Creatures.Mappers;

internal static class EquipmentSlotMapper
{
    public static ContractEquipmentSlot ToContract(this DataEquipmentSlot slot) =>
        slot switch
        {
            DataEquipmentSlot.Helm => ContractEquipmentSlot.Helm,
            DataEquipmentSlot.Chest => ContractEquipmentSlot.Chest,
            DataEquipmentSlot.LeftHand => ContractEquipmentSlot.LeftHand,
            DataEquipmentSlot.RightHand => ContractEquipmentSlot.RightHand,
            DataEquipmentSlot.Boots => ContractEquipmentSlot.Boots,
            DataEquipmentSlot.Necklace => ContractEquipmentSlot.Necklace,
            DataEquipmentSlot.Gloves => ContractEquipmentSlot.Gloves,
            DataEquipmentSlot.LeftRing => ContractEquipmentSlot.LeftRing,
            DataEquipmentSlot.RightRing => ContractEquipmentSlot.RightRing,
            DataEquipmentSlot.Belt => ContractEquipmentSlot.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };

    public static DataEquipmentSlot ToDataModel(this ContractEquipmentSlot slot) =>
        slot switch
        {
            ContractEquipmentSlot.Helm => DataEquipmentSlot.Helm,
            ContractEquipmentSlot.Chest => DataEquipmentSlot.Chest,
            ContractEquipmentSlot.LeftHand => DataEquipmentSlot.LeftHand,
            ContractEquipmentSlot.RightHand => DataEquipmentSlot.RightHand,
            ContractEquipmentSlot.Boots => DataEquipmentSlot.Boots,
            ContractEquipmentSlot.Necklace => DataEquipmentSlot.Necklace,
            ContractEquipmentSlot.Gloves => DataEquipmentSlot.Gloves,
            ContractEquipmentSlot.LeftRing => DataEquipmentSlot.LeftRing,
            ContractEquipmentSlot.RightRing => DataEquipmentSlot.RightRing,
            ContractEquipmentSlot.Belt => DataEquipmentSlot.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
}
