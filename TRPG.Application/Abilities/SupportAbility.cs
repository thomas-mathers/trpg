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

// Block is the only ability whose effect depends on the caster's gear at cast time (parry with a
// shield/melee weapon, brace without one) - this subclass exists so that quirk stays contained to
// Block instead of every BuffAbility carrying a field only one of them ever populates.
public class GuardStanceAbility : BuffAbility
{
    // Used instead of Modifiers when the caster is parry-capable at cast time - see
    // AbilityGearRequirement.IsParryCapable.
    public List<AttributeModifier> ParryCapableModifiers { get; init; } = [];
}
