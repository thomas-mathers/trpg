namespace TRPG.Domain.Models;

public class Shield : Item
{
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public float BlockChance { get; init; }
    public int Defense { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
