using TRPG.Application.Quests.Events;
using TRPG.Quests.Responses;
using ApplicationQuestDialogMode = TRPG.Application.Quests.Events.QuestDialogMode;
using ClientQuestDialogMode = TRPG.Quests.Responses.QuestDialogMode;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestDialogRequestedEventMapper
    : GameClientEventMapper<QuestDialogRequestedEvent>
{
    protected override IGameClientCall Map(QuestDialogRequestedEvent gameEvent) =>
        new GameClientCall<QuestDialogRequested>(
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
                }
            ),
            static (client, arguments) => client.QuestDialogRequested(arguments)
        );
}
