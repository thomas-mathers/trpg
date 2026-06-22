namespace TRPG.Models;

internal class Skill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<Guid> ActiveEffectIds { get; init; } = [];
    public List<Guid> PassiveEffectIds { get; init; } = [];
    public int ApCost { get; init; }
    public int CooldownTurns { get; init; }
}
