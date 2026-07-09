namespace TRPG.Models;

internal enum EquipmentSlot
{
    Helm,
    Chest,
    LeftHand,
    RightHand,
    Boots,
    Necklace,
    Gloves,
    LeftRing,
    RightRing,
    Belt,
}

internal enum ItemRarity
{
    Low,
    Normal,
    Magic,
    Rare,
    Unique,
}

internal class Item
{
    public virtual EquipmentSlot? DefaultSlot => null;
    public string Description { get; init; } = "";
    public int GoldValue { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public virtual bool IsStackable => false;
    public int Level { get; init; }
    public List<ItemModifier> Modifiers { get; init; } = [];
    public string Name { get; init; } = "";
    public ItemRarity Rarity { get; init; }
    public int Weight { get; init; }
    public Guid WorldId { get; init; }
}
