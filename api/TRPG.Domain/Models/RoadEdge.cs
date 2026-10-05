namespace TRPG.Domain.Models;

public enum RoadClass
{
    Avenue,
    Street,
    Lane,
}

public static class RoadClassWidths
{
    public static double Of(RoadClass roadClass) =>
        roadClass switch
        {
            RoadClass.Avenue => 4.5,
            RoadClass.Street => 3.0,
            _ => 1.5,
        };
}

public class RoadEdge
{
    public RoadClass Class { get; init; }
    public Guid FromNodeId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public double Length { get; init; }
    public Guid LocationId { get; init; }
    public Guid ToNodeId { get; init; }
    public Polyline Waypoints { get; init; } = new();
    public Guid WorldId { get; init; }
}
