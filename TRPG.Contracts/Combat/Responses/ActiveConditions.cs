namespace TRPG.Contracts.Combat.Responses;

public record ActiveConditions
{
    public int Blinded { get; init; }
    public int Bleeding { get; init; }
    public int Burning { get; init; }
    public int Disarmed { get; init; }
    public int Frozen { get; init; }
    public int Poisoned { get; init; }
    public int Silenced { get; init; }
    public int Snared { get; init; }
    public int Stunned { get; init; }

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
            .Where(condition => condition.RemainingTurns is > 0)
            .ToArray();
}

public record ActiveCondition(ConditionType Condition, int RemainingTurns);
