namespace TRPG.Models;

internal class BuildingProp {
    public Guid BuildingId { get; init; }
    public Point Coordinates { get; init; } = null!;
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}