using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests.Events;

internal record QuestJournalUpdatedEvent : GameClientEvent
{
    public override string MethodName => "QuestJournalUpdated";
    public override object? Payload => null;
}
