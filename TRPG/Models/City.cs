namespace TRPG.Models;

internal class City {
    public Rectangle Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public int Height { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid CountryId { get; init; }
    public int Width { get; init; }
}