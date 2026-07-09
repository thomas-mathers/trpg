namespace TRPG.Data.Models;

public enum AmountType
{
    Flat,
    Percent,
}

public enum AttributeName
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

public class AttributeModifier : ItemModifier
{
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public AmountType Type { get; init; }
}
