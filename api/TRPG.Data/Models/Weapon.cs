namespace TRPG.Data.Models;

public enum WeaponType
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
    GreatSword,
    GreatAxe,
    GreatHammer,
}

public class Weapon : Item
{
    public int GoldValue { get; init; }
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public WeaponType Type { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int Range { get; init; }
    public int AttacksPerTurn { get; init; }
    public bool IsTwoHanded { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
