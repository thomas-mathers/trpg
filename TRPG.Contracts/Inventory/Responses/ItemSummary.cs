using System.ComponentModel;

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
}

public record ItemSummary(
    Guid ItemId,
    string Name,
    int Weight,
    int Quantity,
    EquipmentSlot? EquippedSlot,
    ItemType Type,
    ItemRarity? Rarity
);

public record ConsumableSummary(
    Guid ItemId,
    string Name,
    int Quantity,
    ResourceType Resource,
    int RestoreAmount
);
