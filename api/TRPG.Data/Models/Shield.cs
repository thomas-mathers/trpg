namespace TRPG.Data.Models;

public class Shield : Item
{
    public int GoldValue { get; init; }
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public float BlockChance { get; init; }
    public int Defense { get; init; }
    public float MagicResistance { get; init; }
    public float FireResistance { get; init; }
    public float IceResistance { get; init; }
    public float LightningResistance { get; init; }
    public float PoisonResistance { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
