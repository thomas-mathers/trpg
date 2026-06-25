namespace TRPG.Models;

internal class AttributeModifier {
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public AmountType Type { get; init; }
}
