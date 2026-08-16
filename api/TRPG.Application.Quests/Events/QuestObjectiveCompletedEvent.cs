using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests.Events;

public record QuestObjectiveCompletedEvent(string ObjectiveName) : GameClientEvent;
