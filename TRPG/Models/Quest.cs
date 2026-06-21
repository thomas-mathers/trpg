namespace TRPG.Models;

internal class Quest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GiverId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int GoldReward { get; init; }
    public int ExperienceReward { get; init; }
    public List<Guid> ItemRewards { get; init; } = [];
    public List<Guid> PrerequisiteQuestIds { get; init; } = [];
}
