using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests;

public record QuestJournalUpdatedEvent : GameClientEvent
{
    public override string MethodName => "QuestJournalUpdated";
    public override object? Payload => null;
}
