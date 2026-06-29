namespace TRPG.Models;

internal class City {
    public Polygon Boundary { get; init; } = null!;
    public Guid CountryId { get; init; }
    public string Description { get; set; } = "";
    public int Height { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsCapital { get; init; }
    public string Name { get; init; } = "";
    public int Width { get; init; }
}