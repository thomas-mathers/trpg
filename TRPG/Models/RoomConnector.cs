namespace TRPG.Models;

internal class RoomConnector : Prop {
    public Guid? DestinationRoomId { get; init; }
    public bool IsLocked { get; set; }
}