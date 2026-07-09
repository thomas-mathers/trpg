namespace TRPG.Models;

internal class Faction
{
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsCityFaction { get; init; }
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}
