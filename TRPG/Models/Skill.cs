namespace TRPG.Models;

internal class Skill {
    public int Cooldown { get; init; }
    public int Cost { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}
