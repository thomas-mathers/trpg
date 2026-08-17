using TRPG.Application.Quests.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestJournalUpdatedEventMapper
    : GameClientEventMapper<QuestJournalUpdatedEvent>
{
    protected override IGameClientCall Map(QuestJournalUpdatedEvent gameEvent) =>
        new GameClientCall(static client => client.QuestJournalUpdated());
}
