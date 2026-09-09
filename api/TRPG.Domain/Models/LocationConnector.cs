namespace TRPG.Domain.Models;

public class LocationConnector
{
    public Guid DestinationLocationId { get; init; }
    public required string DestinationLabel { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";

    // Which way this passage runs, where both ends have a position to measure between. Null
    // outdoors and inside buildings, which are laid out by floor rather than in a plane.
    public CompassDirection? Direction { get; init; }
    public Guid OriginLocationId { get; init; }
    public Guid WorldId { get; init; }
}
