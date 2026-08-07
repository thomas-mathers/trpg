namespace TRPG.Data.Models;

public class CreatureAbility
{
    public string AbilityName { get; init; } = "";
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
}
