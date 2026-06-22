namespace TRPG.Models;

internal class Building {
    public Rectangle Boundary { get; init; } = null!;
    public Guid CityId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}