using TRPG.Application.GameSessions;

namespace TRPG.Application.Quests;

public record QuestJournalUpdatedEvent : GameClientEvent
{
    public override string MethodName => "QuestJournalUpdated";
    public override object? Payload => null;
}
