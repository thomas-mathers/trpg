namespace TRPG.Data.Models;

public enum EquipmentSlot
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

public enum ItemRarity
{
    Low,
    Normal,
    Magic,
    Rare,
    Unique,
}

public enum OwnerType
{
    Creature,
    Container,
    Workstation,
}

public class ItemOwnership
{
    public Guid OwnerId { get; set; }
    public OwnerType OwnerType { get; set; }
    public EquipmentSlot? EquippedSlot { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
}

public class Item
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Weight { get; init; }
    public int Quantity { get; set; }
    public int GoldValue { get; init; }
    public IReadOnlyCollection<ItemModifier> Modifiers { get; init; } = [];
    public ItemOwnership Ownership { get; init; } = new();
}
