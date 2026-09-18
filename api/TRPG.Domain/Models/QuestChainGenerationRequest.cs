namespace TRPG.Domain.Models;

public enum QuestChainGenerationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
}

// Tracks an in-flight LLM-authored quest chain generation so a later seed roll for the same
// player doesn't enqueue a duplicate job while one is already running, and gives the background
// job a terminal state to record instead of leaving the request stuck indefinitely.
public class QuestChainGenerationRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public QuestChainGenerationStatus Status { get; set; } = QuestChainGenerationStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
