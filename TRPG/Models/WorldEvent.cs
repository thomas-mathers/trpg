namespace TRPG.Models;

internal class WorldEvent {
    public DateTime Date { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? StateId { get; init; }
    public List<string> Tags { get; init; } = [];
    public Guid WorldId { get; init; }
}