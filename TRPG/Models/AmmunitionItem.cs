namespace TRPG.Models;

internal enum AmmoType
{
    Arrow,
    Bolt,
}

internal class AmmunitionItem : Item
{
    public override EquipmentSlot? DefaultSlot => EquipmentSlot.LeftHand;
    public override bool IsStackable => true;
    public AmmoType Type { get; init; }
}
