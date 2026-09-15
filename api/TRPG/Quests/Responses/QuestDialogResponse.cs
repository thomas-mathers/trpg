namespace TRPG.Quests.Responses;

[Tapper.TranspilationSource]
public enum QuestDialogMode
{
    Offer,
    TurnIn,
}

[Tapper.TranspilationSource]
public record QuestDialogObjective(
    string Name,
    string Description,
    int RequiredAmount,
    IReadOnlyCollection<string>? ItemNames
);

[Tapper.TranspilationSource]
public record QuestDialogResponse(
    Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestDialogObjective> Objectives,
    QuestDialogMode Mode
);
