namespace TRPG.Models;

internal enum ConditionType
{
    Blinded,
    Bleeding,
    Burning,
    Frozen,
    Poisoned,
    Silenced,
    Snared,
    Stunned,
}

internal class ConditionEffect
{
    public float? Amount { get; init; }
    public ConditionType Condition { get; init; }
    public int Duration { get; init; }
    public AmountType? Type { get; init; }
}
