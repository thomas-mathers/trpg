namespace TRPG.Domain.Models;

public class QuestReputationReward
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid QuestId { get; init; }
    public int Score { get; init; }
    public Guid TargetId { get; init; }
    public ReputationTargetType TargetType { get; init; }
    public Guid WorldId { get; init; }
}
