using TRPG.Domain.Models;

namespace TRPG.Application.Abilities;

public class AttributeEffect
{
    public AttributeName Attribute { get; init; }
    public AmountType AmountType { get; init; }
    public float Amount { get; init; }
    public int Duration { get; init; }
}
