namespace TRPG.Models;

internal enum AmountType {
    Flat,
    Percent
}

internal enum AttributeName {
    CurrentHp,
    MaximumHp,
    CurrentAp,
    MaximumAp,
    Strength,
    Defense,
    Dexterity,
    Endurance,
    Intelligence,
    PhysicalResistance,
    FireResistance,
    IceResistance,
    LightningResistance,
    PoisonResistance,
    MagicResistance,
    MovementSpeed
}

internal class AttributeModifier : ItemModifier {
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public AmountType Type { get; init; }
}