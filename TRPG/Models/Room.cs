namespace TRPG.Models;

internal class Room {
    public Guid BuildingId { get; init; }
    public string Description { get; init; } = "";
    public int FloorNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}