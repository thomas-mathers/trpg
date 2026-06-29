namespace TRPG.Models;

internal class Building {
    public Rectangle Boundary { get; set; } = null!;
    public BuildingType BuildingType { get; init; }
    public Guid CityId { get; init; }
    public string Description { get; set; } = "";
    public Guid? FactionId { get; set; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}