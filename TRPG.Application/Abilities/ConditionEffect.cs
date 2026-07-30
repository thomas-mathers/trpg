using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public enum ConditionType
{
    Blinded,
    Bleeding,
    Burning,
    Disarmed,
    Frozen,
    Poisoned,
    Silenced,
    Snared,
    Stunned,
}

public class DotEffect
{
    public float Amount { get; init; }
    public AmountType AmountType { get; init; }
    public int Duration { get; init; }
}

public class StatusEffect
{
    public ConditionType Condition { get; init; }
    public int Duration { get; init; }
}
