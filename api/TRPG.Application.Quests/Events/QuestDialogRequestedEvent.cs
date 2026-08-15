using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Queries;

namespace TRPG.Application.Quests.Events;

internal enum QuestDialogMode
{
    Offer,
    TurnIn,
}

internal record QuestDialogSnapshot(
    Guid WorldId,
    Guid QuestId,
    string Name,
    string Description,
    int GoldReward,
    IReadOnlyCollection<QuestConversationObjective> Objectives,
    QuestDialogMode Mode
);

internal record QuestDialogRequestedEvent(
    Guid WorldId,
    QuestConversationDetail Quest,
    QuestDialogMode Mode
) : GameClientEvent
{
    public override string MethodName => "QuestDialogRequested";

    public override object? Payload =>
        new QuestDialogSnapshot(
            WorldId,
            Quest.QuestId,
            Quest.Name,
            Quest.Description,
            Quest.GoldReward,
            Quest.Objectives,
            Mode
        );
}
