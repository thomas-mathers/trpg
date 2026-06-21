namespace TRPG.Models;

internal class QuestObjective
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid QuestId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Circle Region { get; init; } = null!;
    public QuestObjectiveType Type { get; init; }
    public QuestTargetType TargetType { get; init; }
    public Guid Target { get; init; }
    public int? Amount { get; init; }
}
