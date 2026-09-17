namespace TRPG.Domain.Models;

public class Quest
{
    public string Description { get; init; } = "";
    public Guid GiverId { get; init; }
    public int GoldReward { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<Guid> ItemRewards { get; init; } = [];
    public string Name { get; init; } = "";
    public List<Guid> PrerequisiteQuestIds { get; init; } = [];
    public List<QuestReputationReward> ReputationRewards { get; init; } = [];
    public Guid WorldId { get; init; }

    // Ordinary quests start revealed. A quest authored with RevealedByFactId starts hidden from
    // the giver's offered quests until the player has made some resolved attempt (any outcome) to
    // learn that fact — a single world has exactly one player, so a plain mutable flag on the
    // quest itself is enough; no per-player attempt tracking is needed.
    public bool IsRevealed { get; set; } = true;
    public Guid? RevealedByFactId { get; init; }
}
