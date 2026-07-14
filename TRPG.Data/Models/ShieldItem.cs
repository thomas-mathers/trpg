namespace TRPG.Data.Models;

public class ShieldItem : Item
{
    public float BlockChance { get; init; }
    public override EquipmentSlot? DefaultSlot => EquipmentSlot.LeftHand;
    public int Defense { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
}
