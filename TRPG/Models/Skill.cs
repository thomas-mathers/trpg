namespace TRPG.Models;

internal class Skill {
    public List<Guid> ActiveEffectIds { get; init; } = [];
    public int ApCost { get; init; }
    public int CooldownTurns { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public List<Guid> PassiveEffectIds { get; init; } = [];
    public Guid WorldId { get; init; }
}