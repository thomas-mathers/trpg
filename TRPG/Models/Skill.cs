namespace TRPG.Models;

internal class Skill {
    public float? AoeRadius { get; init; }
    public int Cooldown { get; init; }
    public int Cost { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public TargetType TargetType { get; init; }
    public Guid WorldId { get; init; }
}
