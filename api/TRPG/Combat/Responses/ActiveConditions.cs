namespace TRPG.Combat.Responses;

[Tapper.TranspilationSource]
public record ActiveConditions
{
    public long? Blinded { get; init; }
    public long? Bleeding { get; init; }
    public long? Burning { get; init; }
    public long? Disarmed { get; init; }
    public long? Frozen { get; init; }
    public long? Poisoned { get; init; }
    public long? Silenced { get; init; }
    public long? Snared { get; init; }
    public long? Stunned { get; init; }
}
