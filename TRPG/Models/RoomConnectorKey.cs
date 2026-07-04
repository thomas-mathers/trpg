namespace TRPG.Models;

internal class RoomConnectorKey {
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public Guid RoomConnectorId { get; init; }
    public Guid WorldId { get; init; }
}
