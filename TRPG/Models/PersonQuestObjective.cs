namespace TRPG.Models;

internal class PersonQuestObjective
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid ObjectiveId { get; init; }
    public int Amount { get; set; }
    public QuestObjective Objective { get; init; } = null!;
}
