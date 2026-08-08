namespace TRPG.Data.Models;

public enum AmmoType
{
    Arrow,
    Bolt,
}

public class Ammunition : Item
{
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public AmmoType Type { get; init; }
}
