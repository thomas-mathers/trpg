namespace TRPG.Models;

internal enum AmountType
{
    Flat,
    Percent,
}

internal enum AttributeName
{
    Hp,
    Ap,
    Mp,
    Strength,
    Defense,
    Dexterity,
    Endurance,
    Stamina,
    Mana,
    Intelligence,
    PhysicalResistance,
    FireResistance,
    IceResistance,
    LightningResistance,
    PoisonResistance,
    MagicResistance,
    MovementSpeed,
}

internal class AttributeModifier : ItemModifier
{
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public AmountType Type { get; init; }
}
