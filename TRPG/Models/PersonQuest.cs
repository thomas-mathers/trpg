namespace TRPG.Models;

internal class PersonQuest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid QuestId { get; init; }
    public QuestStatus Status { get; set; }
    public Quest Quest { get; init; } = null!;
}
