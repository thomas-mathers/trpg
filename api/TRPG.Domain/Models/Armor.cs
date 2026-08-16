namespace TRPG.Domain.Models;

public enum ArmorClass
{
    Cloth,
    Leather,
    Mail,
    Plate,
}

public enum ArmorType
{
    Helm,
    Chest,
    Boots,
    Gloves,
}

public class Armor : Item
{
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public ArmorType Type { get; init; }
    public ArmorClass ArmorClass { get; init; }
    public int Defense { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
