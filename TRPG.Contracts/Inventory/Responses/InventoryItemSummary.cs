using System.Text.Json.Serialization;

namespace TRPG.Contracts.Inventory.Responses;

public enum ResourceType
{
    Hp,
    Ap,
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

public enum WeaponType
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
}

public enum ArmorType
{
    Helm,
    Chest,
    Boots,
    Gloves,
}

public enum AmmoType
{
    Arrow,
    Bolt,
}

public enum AccessoryType
{
    Ring,
    Necklace,
    Belt,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WeaponItemSummary), nameof(WeaponItemSummary))]
[JsonDerivedType(typeof(ArmorItemSummary), nameof(ArmorItemSummary))]
[JsonDerivedType(typeof(ShieldItemSummary), nameof(ShieldItemSummary))]
[JsonDerivedType(typeof(ConsumableItemSummary), nameof(ConsumableItemSummary))]
[JsonDerivedType(typeof(AmmunitionItemSummary), nameof(AmmunitionItemSummary))]
[JsonDerivedType(typeof(AccessoryItemSummary), nameof(AccessoryItemSummary))]
public abstract record InventoryItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity
);

public sealed record WeaponItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    WeaponType Type,
    int MinDamage,
    int MaxDamage
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);

public sealed record ArmorItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    ArmorType Type,
    int Defense
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);

public sealed record ShieldItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    int Defense,
    float BlockChance
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);

public sealed record ConsumableItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    ResourceType Resource,
    int Amount
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);

public sealed record AmmunitionItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    AmmoType Type
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);

public sealed record AccessoryItemSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    AccessoryType Type
) : InventoryItemSummary(ItemId, Name, Quantity, Rarity);
