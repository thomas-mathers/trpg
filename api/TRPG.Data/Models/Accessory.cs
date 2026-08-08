namespace TRPG.Data.Models;

public enum AccessoryType
{
    Ring,
    Necklace,
    Belt,
}

public class Accessory : Item
{
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public AccessoryType Type { get; init; }
}
