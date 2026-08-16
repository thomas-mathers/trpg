using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Events;
using TRPG.Quests.ClientModels;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestObjectiveCompletedEventFormatter
    : GameClientEventFormatter<QuestObjectiveCompletedEvent>
{
    protected override GameClientMessage Format(QuestObjectiveCompletedEvent gameEvent) =>
        new("QuestObjectiveCompleted", new QuestObjectiveCompleted(gameEvent.ObjectiveName));
}
