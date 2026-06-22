namespace TRPG.Models;

internal class City {
    public Circle Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public int Height { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid ProvinceId { get; init; }
    public int Width { get; init; }
}