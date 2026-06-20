namespace TRPG.Models;

public class Country
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Circle Boundary { get; init; } = null!;
}
