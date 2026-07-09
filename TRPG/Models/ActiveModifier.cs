namespace TRPG.Models;

internal class ActiveModifier
{
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int RemainingTurns { get; set; }
    public AmountType Type { get; init; }
}
