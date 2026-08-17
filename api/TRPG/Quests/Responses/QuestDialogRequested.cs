namespace TRPG.Quests.Responses;

[Tapper.TranspilationSource]
public enum QuestDialogMode
{
    Offer,
    TurnIn,
}

[Tapper.TranspilationSource]
public record QuestDialogObjective(string Name, string Description, int RequiredAmount);

[Tapper.TranspilationSource]
public record QuestDialogRequested(
    Guid WorldId,
    Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestDialogObjective> Objectives,
    QuestDialogMode Mode
);
