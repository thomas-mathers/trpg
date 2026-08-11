namespace TRPG.Data.Models;

public class Quest
{
    public string Description { get; init; } = "";
    public Guid GiverId { get; init; }
    public int GoldReward { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<Guid> ItemRewards { get; init; } = [];
    public string Name { get; init; } = "";
    public List<Guid> PrerequisiteQuestIds { get; init; } = [];
    public Guid WorldId { get; init; }
}
