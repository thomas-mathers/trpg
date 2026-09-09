namespace TRPG.Domain.Models;

public enum TrapKind
{
    Mechanical,
    Collapse,
    Slope,
    Water,
}

public class Trigger : Prop
{
    public Guid? TargetId { get; init; }
    public TrapKind? TrapKind { get; init; }
    public bool IsResolved { get; set; }
}
