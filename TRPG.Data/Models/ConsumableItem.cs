namespace TRPG.Data.Models;

public enum ResourceType
{
    Hp,
    Ap,
    Mp,
}

public class ConsumableItem : Item
{
    public int Amount { get; init; }
    public ResourceType Resource { get; init; }
    public int Duration { get; init; }
    public override bool IsStackable => true;
}
