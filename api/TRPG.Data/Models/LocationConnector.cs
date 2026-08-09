namespace TRPG.Data.Models;

public enum LocationDestinationType
{
    District,
    Room,
    Wilderness,
}

public class LocationConnector : Prop
{
    public Guid DestinationLocationId { get; init; }
    public required string DestinationLabel { get; init; }
    public LocationDestinationType DestinationType { get; init; }
    public bool IsLocked { get; set; }
}
