namespace TRPG.Domain.Models;

public class Room
{
    public Rectangle? Bounds { get; init; }
    public Guid BuildingId { get; init; }
    public int Capacity { get; init; }
    public string Description { get; init; } = "";
    public int FloorNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public string Name { get; init; } = "";

    // Null for rooms in ordinary buildings, whose purpose is carried by the building itself.
    public RoomRole? Role { get; init; }
    public Guid WorldId { get; init; }
}
