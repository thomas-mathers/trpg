namespace TRPG.Data.Models;

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

public class ArmorItem : Item
{
    public ArmorClass ArmorClass { get; init; }

    public override EquipmentSlot? DefaultSlot =>
        Type switch
        {
            ArmorType.Helm => EquipmentSlot.Helm,
            ArmorType.Chest => EquipmentSlot.Chest,
            ArmorType.Boots => EquipmentSlot.Boots,
            ArmorType.Gloves => EquipmentSlot.Gloves,
            _ => null,
        };

    public int Defense { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
    public ArmorType Type { get; init; }
}
