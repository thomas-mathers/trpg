namespace TRPG.Data.Models;

public enum AccessoryType
{
    Ring,
    Necklace,
    Belt,
}

public class AccessoryItem : Item
{
    public override EquipmentSlot? DefaultSlot =>
        Type switch
        {
            AccessoryType.Necklace => EquipmentSlot.Necklace,
            AccessoryType.Belt => EquipmentSlot.Belt,
            AccessoryType.Ring => EquipmentSlot.LeftRing,
            _ => null,
        };

    public AccessoryType Type { get; init; }
}
