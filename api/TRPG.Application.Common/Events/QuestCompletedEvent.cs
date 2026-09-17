namespace TRPG.Application.Common.Events;

public sealed record QuestCompletedEvent(Guid PlayerId, Guid WorldId, Guid QuestId) : DomainEvent;
