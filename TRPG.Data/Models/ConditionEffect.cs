namespace TRPG.Data.Models;

public enum ConditionType
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

public class ConditionEffect
{
    public float? Amount { get; init; }
    public ConditionType Condition { get; init; }
    public int Duration { get; init; }
    public AmountType? Type { get; init; }
}
