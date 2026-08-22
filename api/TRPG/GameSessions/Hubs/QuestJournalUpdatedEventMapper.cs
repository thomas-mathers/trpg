using TRPG.Application.Quests.Events;
using TRPG.Quests.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestJournalUpdatedEventMapper
    : GameClientEventMapper<QuestJournalUpdatedEvent>
{
    protected override IGameClientCall Map(QuestJournalUpdatedEvent gameEvent) =>
        new GameClientCall<QuestJournalUpdated>(
            new QuestJournalUpdated(gameEvent.NotificationMessage),
            static (client, questJournal) => client.QuestJournalUpdated(questJournal)
        );
}
