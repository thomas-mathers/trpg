using TRPG.Application.Quests.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class QuestJournalUpdatedEventFormatter
    : GameClientEventFormatter<QuestJournalUpdatedEvent>
{
    protected override GameClientMessage Format(QuestJournalUpdatedEvent gameEvent) =>
        new("QuestJournalUpdated", null);
}
