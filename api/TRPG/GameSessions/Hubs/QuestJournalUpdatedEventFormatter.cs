using TRPG.Application.Quests.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestJournalUpdatedEventFormatter
    : GameClientEventFormatter<QuestJournalUpdatedEvent>
{
    protected override Task Dispatch(IGameClient client, QuestJournalUpdatedEvent gameEvent) =>
        client.QuestJournalUpdated();
}
