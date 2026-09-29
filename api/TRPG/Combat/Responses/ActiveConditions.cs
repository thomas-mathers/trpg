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

    public IReadOnlyCollection<ActiveCondition> ToEntries() =>
        new[]
        {
            new ActiveCondition(ConditionType.Blinded, Blinded),
            new ActiveCondition(ConditionType.Bleeding, Bleeding),
            new ActiveCondition(ConditionType.Burning, Burning),
            new ActiveCondition(ConditionType.Disarmed, Disarmed),
            new ActiveCondition(ConditionType.Frozen, Frozen),
            new ActiveCondition(ConditionType.Poisoned, Poisoned),
            new ActiveCondition(ConditionType.Silenced, Silenced),
            new ActiveCondition(ConditionType.Snared, Snared),
            new ActiveCondition(ConditionType.Stunned, Stunned),
        }
            .Where(condition => condition.ExpiresAtGameTimeMilliseconds is not null)
            .ToArray();
}

public record ActiveCondition(ConditionType Condition, long? ExpiresAtGameTimeMilliseconds);
