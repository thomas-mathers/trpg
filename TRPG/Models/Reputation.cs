namespace TRPG.Models;

internal enum ReputationTargetType {
    Faction,
    Person
}

internal class Reputation {
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid TargetId { get; init; }
    public ReputationTargetType TargetType { get; init; }
    public int Score { get; set; }
    public Guid WorldId { get; init; }
}