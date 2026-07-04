namespace TRPG.Models;

internal enum ArmorClass {
    Cloth,
    Leather,
    Mail,
    Plate
}

internal enum ArmorType {
    Helm,
    Chest,
    Boots,
    Gloves
}

internal class ArmorItem : Item {
    public ArmorClass ArmorClass { get; init; }

    public override EquipmentSlot? DefaultSlot => Type switch {
        ArmorType.Helm => EquipmentSlot.Helm,
        ArmorType.Chest => EquipmentSlot.Chest,
        ArmorType.Boots => EquipmentSlot.Boots,
        ArmorType.Gloves => EquipmentSlot.Gloves,
        _ => null
    };

    public int Defense { get; init; }
    public int DurabilityCurrent { get; set; }
    public int DurabilityMax { get; init; }
    public ArmorType Type { get; init; }
}