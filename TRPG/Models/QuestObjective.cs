namespace TRPG.Models;

internal enum QuestObjectiveType {
    Kill,
    Collect,
    Explore,
    Speak
}

internal enum QuestTargetType {
    Person,
    Item,
    City,
    Building,
    Race
}

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