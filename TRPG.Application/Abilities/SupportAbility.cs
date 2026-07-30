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

    // Used instead of Modifiers when the caster is parry-capable (shield or melee weapon
    // equipped) at cast time — see AbilityGearRequirement.IsParryCapable. Empty for every
    // buff that doesn't vary by equipped gear.
    public List<AttributeModifier> ParryCapableModifiers { get; init; } = [];
}
