namespace TRPG.Models;

internal class RoomConnector : Prop {
    public Guid? DestinationRoomId { get; init; }
    public Guid? KeyItemId { get; init; }
}
