using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests;

public record QuestObjectiveCompletedEvent(string ObjectiveName) : GameClientEvent
{
    public override string MethodName => "QuestObjectiveCompleted";
    public override object? Payload => new { ObjectiveName };
}
