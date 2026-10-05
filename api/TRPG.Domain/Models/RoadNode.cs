namespace TRPG.Domain.Models;

public enum RoadNodeKind
{
    Junction,
    Port,
}

public class RoadNode
{
    public Guid? ConnectorId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public RoadNodeKind Kind { get; init; }
    public Guid LocationId { get; init; }
    public Guid WorldId { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}
