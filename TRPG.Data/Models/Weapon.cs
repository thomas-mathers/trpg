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
}

public record Weapon : Item
{
    public int GoldValue { get; init; }
    public int Level { get; init; }
    public ItemRarity Rarity { get; init; }
    public WeaponType Type { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int Range { get; init; }
    public int AttackSpeed { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
