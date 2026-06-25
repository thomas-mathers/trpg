namespace TRPG.Models;

internal class Support : Skill {
    public int? Duration { get; init; }
    public List<AttributeModifier> Modifiers { get; init; } = [];
}
