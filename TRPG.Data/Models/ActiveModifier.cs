namespace TRPG.Data.Models;

public class ActiveModifier
{
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int RemainingTurns { get; set; }
    public AmountType Type { get; init; }
}
