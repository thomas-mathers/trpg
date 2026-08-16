using TRPG.Domain.Models;

namespace TRPG.Application.Abilities;

public enum TargetType
{
    Single,
    Aoe,
    Self,
}

public class HotEffect
{
    public float Amount { get; init; }
    public AmountType AmountType { get; init; }
    public int Duration { get; init; }
}

public class SupportAbility : Ability
{
    public TargetType TargetType { get; init; }
    public float HealAmount { get; init; }
    public AmountType HealAmountType { get; init; }
    public List<HotEffect> Hots { get; init; } = [];
    public List<AttributeEffect> Buffs { get; init; } = [];
    public List<AttributeEffect> BuffsWhileParrying { get; init; } = [];
}
