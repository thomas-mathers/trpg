namespace TRPG.Models;

internal enum QuestStatus {
    Accepted,
    Completed,
    Failed,
    Abandoned
}

internal class CreatureQuest {
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Quest Quest { get; init; } = null!;
    public Guid QuestId { get; init; }
    public QuestStatus Status { get; set; }
    public Guid WorldId { get; init; }
}
