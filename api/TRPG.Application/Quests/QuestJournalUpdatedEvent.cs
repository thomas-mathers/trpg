using TRPG.Application.GameSessions;

namespace TRPG.Application.Quests;

public record QuestJournalUpdatedEvent : GameTurnEvent
{
    public override string MethodName => "QuestJournalUpdated";
    public override object? Payload => null;
}
