namespace TRPG.Domain.Models;

public class ReputationLogEntry
{
    public Guid CreatureId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int DeltaScore { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Reason { get; init; }
    public Guid TargetId { get; init; }
    public ReputationTargetType TargetType { get; init; }
    public Guid WorldId { get; init; }
}
