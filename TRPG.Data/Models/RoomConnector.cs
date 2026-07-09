namespace TRPG.Data.Models;

public class RoomConnector : Prop
{
    public Guid? DestinationRoomId { get; init; }
    public bool IsLocked { get; set; }
}
