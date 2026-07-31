using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public enum TargetType
{
    Single,
    Aoe,
    Self,
}

public abstract class SupportAbility : Ability
{
    public TargetType TargetType { get; init; }
}

public class InstantHealAbility : SupportAbility
{
    public int Amount { get; init; }
}

public class HealOverTimeAbility : SupportAbility
{
    public int AmountPerTurn { get; init; }
    public int Duration { get; init; }
}

public class BuffAbility : SupportAbility
{
    public int Duration { get; init; }
    public List<AttributeModifier> Modifiers { get; init; } = [];
}

public class GuardStanceAbility : BuffAbility
{
    public List<AttributeModifier> ParryCapableModifiers { get; init; } = [];
}
