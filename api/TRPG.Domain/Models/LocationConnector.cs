namespace TRPG.Domain.Models;

public enum StairDirection
{
    Up,
    Down,
}

public class LocationConnector : Connector
{
    public double ArrivalAngle { get; set; }
    public Guid DestinationLocationId { get; init; }
    public required string DestinationLabel { get; init; }
    public string Description { get; init; } = "";
    public CompassDirection? Direction { get; init; }
    public double ExitAngle { get; set; }
    public string Name { get; init; } = "";
    public Guid OriginLocationId { get; init; }
    public Polyline? Path { get; init; }
    public StairDirection? StairDirection { get; set; }
}
