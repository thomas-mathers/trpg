namespace TRPG.Data.Models;

public class LocationConnector : Prop
{
    public Guid DestinationLocationId { get; init; }
    public required string DestinationLabel { get; init; }
    public bool IsLocked { get; set; }
}
