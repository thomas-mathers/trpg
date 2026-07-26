namespace TRPG.Data.Models;

public enum ResourceType
{
    Hp,
    Ap,
    Mp,
}

public record Consumable : Item
{
    public int GoldValue { get; init; }
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public int RestoreAmount { get; init; }
    public ResourceType Resource { get; init; }
    public int Duration { get; init; }
}
