namespace TRPG.Models;

public class Faction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
}
