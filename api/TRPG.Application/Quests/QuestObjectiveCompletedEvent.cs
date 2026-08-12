using TRPG.Application.GameSessions;

namespace TRPG.Application.Quests;

public record QuestObjectiveCompletedEvent(string ObjectiveName) : GameTurnEvent
{
    public override string MethodName => "QuestObjectiveCompleted";
    public override object? Payload => new { ObjectiveName };
}
