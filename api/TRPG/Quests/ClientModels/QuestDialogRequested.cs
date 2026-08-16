namespace TRPG.Quests.ClientModels;

public enum QuestDialogMode
{
    Offer,
    TurnIn,
}

public record QuestDialogObjective(string Name, string Description, int RequiredAmount);

public record QuestDialogRequested(
    Guid WorldId,
    Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestDialogObjective> Objectives,
    QuestDialogMode Mode
);
