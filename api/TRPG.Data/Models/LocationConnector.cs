namespace TRPG.Data.Models;

public class LocationConnector
{
    public Guid DestinationLocationId { get; init; }
    public required string DestinationLabel { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid OriginLocationId { get; init; }
    public Guid WorldId { get; init; }
}
