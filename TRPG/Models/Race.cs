namespace TRPG.Models;

internal class Race {
    public string CultureStyle { get; init; } = "";
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}