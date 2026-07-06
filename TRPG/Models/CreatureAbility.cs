namespace TRPG.Models;

internal class CreatureAbility {
    public string AbilityName { get; init; } = "";
    public int Cooldown { get; set; }
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
}
