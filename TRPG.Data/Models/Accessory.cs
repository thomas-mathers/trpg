namespace TRPG.Data.Models;

public enum AccessoryType
{
    Ring,
    Necklace,
    Belt,
}

public record Accessory : Item
{
    public int GoldValue { get; init; }
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public AccessoryType Type { get; init; }
}
