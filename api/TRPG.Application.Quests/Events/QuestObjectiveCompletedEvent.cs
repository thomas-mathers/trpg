using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests.Events;

public record QuestObjectiveCompletedEvent(Guid WorldId, string ObjectiveName)
    : GameClientEvent(WorldId);
