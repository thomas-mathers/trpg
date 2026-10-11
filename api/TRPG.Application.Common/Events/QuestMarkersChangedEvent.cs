namespace TRPG.Application.Common.Events;

public sealed record QuestMarkersChangedEvent(Guid PlayerId, Guid WorldId) : DomainEvent;
