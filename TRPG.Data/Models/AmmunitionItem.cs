namespace TRPG.Data.Models;

public enum AmmoType
{
    Arrow,
    Bolt,
}

public class AmmunitionItem : Item
{
    public override EquipmentSlot? DefaultSlot => EquipmentSlot.LeftHand;
    public override bool IsStackable => true;
    public AmmoType Type { get; init; }
}
