namespace TRPG.Data.Models;

public class Room
{
    public Guid BuildingId { get; init; }
    public int Capacity { get; init; }
    public string Description { get; init; } = "";
    public int FloorNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}
