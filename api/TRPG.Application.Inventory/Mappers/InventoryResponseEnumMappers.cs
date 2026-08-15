using ContractArmorClass = TRPG.Contracts.Inventory.Responses.ArmorClass;
using ContractCombatSpeedType = TRPG.Contracts.Inventory.Responses.CombatSpeedType;
using ContractEquipmentSlot = TRPG.Contracts.Inventory.Responses.EquipmentSlot;
using ContractItemRarity = TRPG.Contracts.Inventory.Responses.ItemRarity;
using ContractLeechType = TRPG.Contracts.Inventory.Responses.LeechType;
using ContractProcTrigger = TRPG.Contracts.Inventory.Responses.ProcTrigger;
using ContractSpecialHitType = TRPG.Contracts.Inventory.Responses.SpecialHitType;
using DataArmorClass = TRPG.Data.Models.ArmorClass;
using DataCombatSpeedType = TRPG.Data.Models.CombatSpeedType;
using DataEquipmentSlot = TRPG.Data.Models.EquipmentSlot;
using DataItemRarity = TRPG.Data.Models.ItemRarity;
using DataLeechType = TRPG.Data.Models.LeechType;
using DataProcTrigger = TRPG.Data.Models.ProcTrigger;
using DataSpecialHitType = TRPG.Data.Models.SpecialHitType;

namespace TRPG.Application.Inventory.Mappers;

internal static class InventoryResponseEnumMappers
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

    public static ContractItemRarity ToContract(this DataItemRarity rarity) =>
        rarity switch
        {
            DataItemRarity.Low => ContractItemRarity.Low,
            DataItemRarity.Normal => ContractItemRarity.Normal,
            DataItemRarity.Magic => ContractItemRarity.Magic,
            DataItemRarity.Rare => ContractItemRarity.Rare,
            DataItemRarity.Unique => ContractItemRarity.Unique,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
        };

    public static ContractArmorClass ToContract(this DataArmorClass armorClass) =>
        armorClass switch
        {
            DataArmorClass.Cloth => ContractArmorClass.Cloth,
            DataArmorClass.Leather => ContractArmorClass.Leather,
            DataArmorClass.Mail => ContractArmorClass.Mail,
            DataArmorClass.Plate => ContractArmorClass.Plate,
            _ => throw new ArgumentOutOfRangeException(nameof(armorClass), armorClass, null),
        };

    public static ContractCombatSpeedType ToContract(this DataCombatSpeedType type) =>
        type switch
        {
            DataCombatSpeedType.IncreasedAttackSpeed =>
                ContractCombatSpeedType.IncreasedAttackSpeed,
            DataCombatSpeedType.FasterCastRate => ContractCombatSpeedType.FasterCastRate,
            DataCombatSpeedType.FasterHitRecovery => ContractCombatSpeedType.FasterHitRecovery,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractLeechType ToContract(this DataLeechType type) =>
        type switch
        {
            DataLeechType.Life => ContractLeechType.Life,
            DataLeechType.Mana => ContractLeechType.Mana,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractSpecialHitType ToContract(this DataSpecialHitType type) =>
        type switch
        {
            DataSpecialHitType.CrushingBlow => ContractSpecialHitType.CrushingBlow,
            DataSpecialHitType.DeadlyStrike => ContractSpecialHitType.DeadlyStrike,
            DataSpecialHitType.OpenWounds => ContractSpecialHitType.OpenWounds,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractProcTrigger ToContract(this DataProcTrigger trigger) =>
        trigger switch
        {
            DataProcTrigger.OnStriking => ContractProcTrigger.OnStriking,
            DataProcTrigger.WhenStruck => ContractProcTrigger.WhenStruck,
            DataProcTrigger.OnKill => ContractProcTrigger.OnKill,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };
}
