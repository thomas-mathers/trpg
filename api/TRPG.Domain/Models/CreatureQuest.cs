namespace TRPG.Domain.Models;

public enum QuestStatus
{
    Accepted,
    ReadyToComplete,
    Completed,
    Failed,
    Abandoned,
}

public class CreatureQuest
{
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsTracked { get; set; } = true;
    public Quest Quest { get; init; } = null!;
    public Guid QuestId { get; init; }
    public QuestStatus Status { get; set; }
    public Guid WorldId { get; init; }
}
