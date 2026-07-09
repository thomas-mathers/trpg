namespace TRPG.Models;

internal enum ReputationTargetType
{
    Faction,
    Creature,
}

internal class Reputation
{
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Score { get; set; }
    public Guid TargetId { get; init; }
    public ReputationTargetType TargetType { get; init; }
    public Guid WorldId { get; init; }
}
