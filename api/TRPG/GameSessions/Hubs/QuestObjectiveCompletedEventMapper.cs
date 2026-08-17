using TRPG.Application.Quests.Events;
using TRPG.Quests.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestObjectiveCompletedEventMapper
    : GameClientEventMapper<QuestObjectiveCompletedEvent>
{
    protected override IGameClientCall Map(QuestObjectiveCompletedEvent gameEvent) =>
        new GameClientCall<QuestObjectiveCompleted>(
            new QuestObjectiveCompleted(gameEvent.ObjectiveName),
            static (client, arguments) => client.QuestObjectiveCompleted(arguments)
        );
}
