namespace TRPG.Domain.Models;

public enum AmountType
{
    Flat,
    Percent,
}

public enum AttributeName
{
    MaximumHp,
    MaximumAp,
    MaximumMp,
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
    public AmountType AmountType { get; init; }
}
