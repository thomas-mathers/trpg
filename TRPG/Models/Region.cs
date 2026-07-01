namespace TRPG.Models;

internal enum RegionType {
    Urban,
    Rural
}

internal class Region {
    public Polygon Boundary { get; init; } = null!;
    public Guid CountryId { get; init; }
    public string Description { get; init; } = "";
    public int Height { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsCapital { get; init; }
    public string Name { get; init; } = "";
    public RegionType RegionType { get; init; }
    public int Width { get; init; }
}
