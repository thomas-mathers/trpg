namespace TRPG.Models;

internal class SupportAbility : Ability
{
    public int? Duration { get; init; }
    public List<AttributeModifier> Modifiers { get; init; } = [];
}
