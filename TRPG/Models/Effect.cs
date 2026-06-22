namespace TRPG.Models;

internal class Effect {
    public EffectApplicationMode ApplicationMode { get; init; }
    public string Description { get; init; } = "";
    public int? Duration { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public EffectStat Stat { get; init; }
    public EffectType Type { get; init; }
    public float Value { get; init; }
    public Guid WorldId { get; init; }
}