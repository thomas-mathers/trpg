namespace TRPG.Models;

internal class QuestObjective {
    public int? Amount { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid QuestId { get; init; }
    public Circle Region { get; init; } = null!;
    public Guid Target { get; init; }
    public QuestTargetType TargetType { get; init; }
    public QuestObjectiveType Type { get; init; }
}