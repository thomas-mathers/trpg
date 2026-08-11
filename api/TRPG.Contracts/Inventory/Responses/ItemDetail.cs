using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TRPG.Contracts.Inventory.Responses;

public enum ResourceType
{
    [Description("HP")]
    Hp,

    [Description("AP")]
    Ap,

    [Description("MP")]
    Mp,
}

public enum ItemRarity
{
    Low,
    Normal,
    Magic,
    Rare,
    Unique,
}

public enum EquipmentSlot
{
    Helm,
    Chest,

    [Description("Left Hand")]
    LeftHand,

    [Description("Right Hand")]
    RightHand,
    Boots,
    Necklace,
    Gloves,

    [Description("Left Ring")]
    LeftRing,

    [Description("Right Ring")]
    RightRing,
    Belt,
}

public enum ItemType
{
    Dagger,
    Sword,
    Axe,
    Mace,
    Hammer,
    Staff,
    Wand,
    Bow,
    Crossbow,
    Javelin,

    [Description("Great Sword")]
    GreatSword,

    [Description("Great Axe")]
    GreatAxe,

    [Description("Great Hammer")]
    GreatHammer,
    Helm,
    Chest,
    Boots,
    Gloves,
    Arrow,
    Bolt,
    Ring,
    Necklace,
    Belt,
    Shield,
    Consumable,
    Gold,
    Key,
}

public enum ArmorClass
{
    Cloth,
    Leather,
    Mail,
    Plate,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WeaponDetail), "Weapon")]
[JsonDerivedType(typeof(ArmorDetail), "Armor")]
[JsonDerivedType(typeof(ShieldDetail), "Shield")]
[JsonDerivedType(typeof(AccessoryDetail), "Accessory")]
[JsonDerivedType(typeof(AmmunitionDetail), "Ammunition")]
[JsonDerivedType(typeof(ConsumableItemDetail), "Consumable")]
[JsonDerivedType(typeof(GoldDetail), "Gold")]
[JsonDerivedType(typeof(KeyDetail), "Key")]
public abstract record ItemDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable
);

public sealed record WeaponDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable,
    int MinDamage,
    int MaxDamage,
    int Range,
    int AttacksPerTurn,
    bool IsTwoHanded,
    int DurabilityCurrent,
    int DurabilityMax
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record ArmorDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable,
    int Defense,
    ArmorClass ArmorClass,
    int DurabilityCurrent,
    int DurabilityMax
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record ShieldDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable,
    float BlockChance,
    int Defense,
    int DurabilityCurrent,
    int DurabilityMax
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record AccessoryDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record AmmunitionDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record ConsumableItemDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable,
    ResourceType Resource,
    int RestoreAmount,
    int Duration
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record GoldDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public sealed record KeyDetail(
    Guid ItemId,
    string Name,
    string Description,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity,
    int GoldValue,
    IReadOnlyList<ItemModifierSummary> Modifiers,
    bool IsStackable
)
    : ItemDetail(
        ItemId,
        Name,
        Description,
        Weight,
        Quantity,
        EquippedSlot,
        Type,
        Rarity,
        GoldValue,
        Modifiers,
        IsStackable
    );

public record ConsumableSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ResourceType Resource,
    int RestoreAmount
);
