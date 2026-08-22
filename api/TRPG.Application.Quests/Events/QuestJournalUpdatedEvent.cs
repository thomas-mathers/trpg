using TRPG.Application.Common.Events;

namespace TRPG.Application.Quests.Events;

public record QuestJournalUpdatedEvent(string? NotificationMessage = null) : GameClientEvent;
