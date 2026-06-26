namespace TRPG.Models;

internal class Country {
    public Polygon Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}