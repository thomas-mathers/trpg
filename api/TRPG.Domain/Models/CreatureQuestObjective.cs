namespace TRPG.Domain.Models;

public class CreatureQuestObjective
{
    public int Amount { get; set; }
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public QuestObjective Objective { get; init; } = null!;
    public Guid ObjectiveId { get; init; }
    public Guid WorldId { get; init; }
}
