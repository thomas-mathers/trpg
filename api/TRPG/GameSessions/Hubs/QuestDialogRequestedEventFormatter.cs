using TRPG.Application.Quests.Events;
using TRPG.Quests.ClientModels;
using ApplicationQuestDialogMode = TRPG.Application.Quests.Events.QuestDialogMode;
using ClientQuestDialogMode = TRPG.Quests.ClientModels.QuestDialogMode;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestDialogRequestedEventFormatter
    : GameClientEventFormatter<QuestDialogRequestedEvent>
{
    protected override GameClientMessage Format(QuestDialogRequestedEvent gameEvent) =>
        new(
            "QuestDialogRequested",
            new QuestDialogRequested(
                gameEvent.WorldId,
                gameEvent.Quest.QuestId,
                gameEvent.Quest.Name,
                gameEvent.Quest.Description,
                gameEvent.Quest.GoldReward,
                gameEvent
                    .Quest.Objectives.Select(objective => new QuestDialogObjective(
                        objective.Name,
                        objective.Description,
                        objective.RequiredAmount
                    ))
                    .ToArray(),
                gameEvent.Mode switch
                {
                    ApplicationQuestDialogMode.Offer => ClientQuestDialogMode.Offer,
                    ApplicationQuestDialogMode.TurnIn => ClientQuestDialogMode.TurnIn,
                    _ => throw new ArgumentOutOfRangeException(nameof(gameEvent)),
                }
            )
        );
}
