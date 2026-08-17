using TRPG.Application.Quests.Events;
using TRPG.Quests.ClientModels;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestObjectiveCompletedEventFormatter
    : GameClientEventFormatter<QuestObjectiveCompletedEvent>
{
    protected override Task Dispatch(IGameClient client, QuestObjectiveCompletedEvent gameEvent) =>
        client.QuestObjectiveCompleted(new QuestObjectiveCompleted(gameEvent.ObjectiveName));
}
