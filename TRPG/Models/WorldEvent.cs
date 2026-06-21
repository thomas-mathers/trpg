namespace TRPG.Models;

internal class WorldEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Description { get; init; } = "";
    public List<string> Tags { get; init; } = [];
    public Circle Region { get; init; } = null!;
    public DateTime Date { get; init; }
}
