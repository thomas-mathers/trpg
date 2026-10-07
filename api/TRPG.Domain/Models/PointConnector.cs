namespace TRPG.Domain.Models;

public class PointConnector : Connector
{
    public bool Bidirectional { get; init; }
    public Guid LocationId { get; init; }
    public RoadClass? RoadClass { get; init; }
    public Polyline Waypoints { get; init; } = new();
}
