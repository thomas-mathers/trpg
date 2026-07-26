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
[JsonDerivedType(typeof(WeaponSummary), nameof(WeaponSummary))]
[JsonDerivedType(typeof(ArmorSummary), nameof(ArmorSummary))]
[JsonDerivedType(typeof(ShieldSummary), nameof(ShieldSummary))]
[JsonDerivedType(typeof(ConsumableSummary), nameof(ConsumableSummary))]
[JsonDerivedType(typeof(AmmunitionSummary), nameof(AmmunitionSummary))]
[JsonDerivedType(typeof(AccessorySummary), nameof(AccessorySummary))]
[JsonDerivedType(typeof(GoldSummary), nameof(GoldSummary))]
public abstract record InventoryItemSummary(Guid ItemId, string Name, int Quantity);

public sealed record WeaponSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    WeaponType Type,
    int MinDamage,
    int MaxDamage
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record ArmorSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    ArmorType Type,
    int Defense
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record ShieldSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    int Defense,
    float BlockChance
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record ConsumableSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    ResourceType Resource,
    int RestoreAmount
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record AmmunitionSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    AmmoType Type
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record AccessorySummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ItemRarity Rarity,
    AccessoryType Type
) : InventoryItemSummary(ItemId, Name, Quantity);

public sealed record GoldSummary(Guid ItemId, string Name, int Quantity)
    : InventoryItemSummary(ItemId, Name, Quantity);
