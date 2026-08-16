using ContractAmountType = TRPG.Contracts.Combat.Responses.AmountType;
using ContractArmorClass = TRPG.Contracts.Inventory.Responses.ArmorClass;
using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using ContractCombatSpeedType = TRPG.Contracts.Inventory.Responses.CombatSpeedType;
using ContractDamageType = TRPG.Contracts.Combat.Responses.DamageType;
using ContractEquipmentSlot = TRPG.Contracts.Inventory.Responses.EquipmentSlot;
using ContractItemRarity = TRPG.Contracts.Inventory.Responses.ItemRarity;
using ContractLeechType = TRPG.Contracts.Inventory.Responses.LeechType;
using ContractProcTrigger = TRPG.Contracts.Inventory.Responses.ProcTrigger;
using ContractResourceType = TRPG.Contracts.Inventory.Responses.ResourceType;
using ContractSpecialHitType = TRPG.Contracts.Inventory.Responses.SpecialHitType;
using DataAmountType = TRPG.Data.Models.AmountType;
using DataArmorClass = TRPG.Data.Models.ArmorClass;
using DataAttributeName = TRPG.Data.Models.AttributeName;
using DataCombatSpeedType = TRPG.Data.Models.CombatSpeedType;
using DataDamageType = TRPG.Data.Models.DamageType;
using DataEquipmentSlot = TRPG.Data.Models.EquipmentSlot;
using DataItemRarity = TRPG.Data.Models.ItemRarity;
using DataLeechType = TRPG.Data.Models.LeechType;
using DataProcTrigger = TRPG.Data.Models.ProcTrigger;
using DataResourceType = TRPG.Data.Models.ResourceType;
using DataSpecialHitType = TRPG.Data.Models.SpecialHitType;

namespace TRPG.Creatures.Mappers;

internal static class ItemResponseEnumMappers
{
    public static ContractDamageType ToContract(this DataDamageType type) =>
        type switch
        {
            DataDamageType.Physical => ContractDamageType.Physical,
            DataDamageType.Fire => ContractDamageType.Fire,
            DataDamageType.Ice => ContractDamageType.Ice,
            DataDamageType.Lightning => ContractDamageType.Lightning,
            DataDamageType.Poison => ContractDamageType.Poison,
            DataDamageType.Magic => ContractDamageType.Magic,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractAttributeName ToContract(this DataAttributeName attribute) =>
        attribute switch
        {
            DataAttributeName.MaximumHp => ContractAttributeName.MaximumHp,
            DataAttributeName.MaximumAp => ContractAttributeName.MaximumAp,
            DataAttributeName.MaximumMp => ContractAttributeName.MaximumMp,
            DataAttributeName.Strength => ContractAttributeName.Strength,
            DataAttributeName.Defense => ContractAttributeName.Defense,
            DataAttributeName.Dexterity => ContractAttributeName.Dexterity,
            DataAttributeName.Endurance => ContractAttributeName.Endurance,
            DataAttributeName.Stamina => ContractAttributeName.Stamina,
            DataAttributeName.Mana => ContractAttributeName.Mana,
            DataAttributeName.Intelligence => ContractAttributeName.Intelligence,
            DataAttributeName.PhysicalResistance => ContractAttributeName.PhysicalResistance,
            DataAttributeName.FireResistance => ContractAttributeName.FireResistance,
            DataAttributeName.IceResistance => ContractAttributeName.IceResistance,
            DataAttributeName.LightningResistance => ContractAttributeName.LightningResistance,
            DataAttributeName.PoisonResistance => ContractAttributeName.PoisonResistance,
            DataAttributeName.MagicResistance => ContractAttributeName.MagicResistance,
            DataAttributeName.MovementSpeed => ContractAttributeName.MovementSpeed,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

    public static ContractAmountType ToContract(this DataAmountType type) =>
        type switch
        {
            DataAmountType.Flat => ContractAmountType.Flat,
            DataAmountType.Percent => ContractAmountType.Percent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractResourceType ToContract(this DataResourceType resource) =>
        resource switch
        {
            DataResourceType.Hp => ContractResourceType.Hp,
            DataResourceType.Ap => ContractResourceType.Ap,
            DataResourceType.Mp => ContractResourceType.Mp,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
        };

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
