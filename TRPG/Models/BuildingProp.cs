namespace TRPG.Models;

public class BuildingProp
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BuildingId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Point Coordinates { get; init; } = null!;
}
