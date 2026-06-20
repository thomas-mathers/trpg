namespace TRPG.Models;

public class City
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProvinceId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public Circle Boundary { get; init; } = null!;
}
