namespace TRPG.Models;

internal class BuildingRoom {
    public Guid BuildingId { get; init; }
    public Rectangle Boundary { get; init; } = null!;
    public int FloorNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}
