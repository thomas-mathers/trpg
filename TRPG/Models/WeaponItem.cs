namespace TRPG.Models;

internal enum WeaponType {
    Dagger,
    Sword,
    Axe,
    Mace,
    Hammer,
    Staff,
    Wand,
    Bow,
    Crossbow,
    Javelin
}

internal class WeaponItem : Item {
    public int AttackSpeed { get; init; }
    public override EquipmentSlot? DefaultSlot => EquipmentSlot.RightHand;
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
    public override bool IsStackable => Type == WeaponType.Javelin;
    public int MaxDamage { get; init; }
    public int MinDamage { get; init; }
    public int Range { get; init; }
    public WeaponType Type { get; init; }
}