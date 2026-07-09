namespace TRPG.Data.Models;

public class ConsumableItem : Item
{
    public int Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int Duration { get; init; }
    public override bool IsStackable => true;
}
