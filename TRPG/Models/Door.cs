namespace TRPG.Models;

internal class Door : Prop {
    public Guid? DestinationRoomId { get; init; }
    public Guid? KeyItemId { get; init; }
}
