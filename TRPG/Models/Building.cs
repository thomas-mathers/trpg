namespace TRPG.Models;

public class Building
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CityId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Rectangle Boundary { get; init; } = null!;
}
