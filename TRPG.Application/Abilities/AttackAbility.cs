using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public enum AttackTargetType
{
    Single,
    Aoe,
}

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

public class StatusEffect
{
    public ConditionType Condition { get; init; }
    public int Duration { get; init; }
}

public class DotEffect
{
    public float Amount { get; init; }
    public AmountType AmountType { get; init; }
    public int Duration { get; init; }
}

public class AttackAbility : Ability
{
    public AttackTargetType TargetType { get; init; }
    public List<DotEffect> Dots { get; init; } = [];
    public List<StatusEffect> Conditions { get; init; } = [];
    public List<AttributeEffect> Debuffs { get; init; } = [];
    public float DamageAmount { get; init; }
    public AmountType DamageAmountType { get; init; }
    public DamageType DamageType { get; init; }
    public CreatureType? BonusTargetCreatureType { get; set; }
    public float BonusDamageMultiplier { get; set; } = 1f;
}
