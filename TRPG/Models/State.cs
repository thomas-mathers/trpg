namespace TRPG.Models;

internal class State
{
    public Polygon Boundary { get; init; } = null!;
    public Point Center { get; init; } = null!;
    public Guid CountryId { get; init; }
    public string Description { get; init; } = "";
    public int Height { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public int Width { get; init; }
    public Guid WorldId { get; init; }
}
