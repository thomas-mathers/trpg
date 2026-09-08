namespace TRPG.Domain.Models;

public class Room
{
    public Guid BuildingId { get; init; }
    public int Capacity { get; init; }
    public string Description { get; init; } = "";
    public int FloorNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public string Name { get; init; } = "";

    // Dungeons are laid out in a plane so their rooms can be drawn on a map; rooms in a building
    // have no position because a building's shape is its floors.
    public Point? Position { get; init; }
    public Guid WorldId { get; init; }
}
