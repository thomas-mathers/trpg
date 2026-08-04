namespace TRPG.Data.Models;

public class RoomConnector : Prop
{
    public Guid DestinationLocationId { get; init; }
    public bool IsLocked { get; set; }
}
