using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests.Events;

internal record QuestObjectiveCompletedEvent(string ObjectiveName) : GameClientEvent
{
    public override string MethodName => "QuestObjectiveCompleted";
    public override object? Payload => new { ObjectiveName };
}
