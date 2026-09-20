namespace TRPG.Application.Common.Events;

public sealed record TriggerActivatedEvent(Guid PlayerId, Guid WorldId, Guid TriggerId)
    : DomainEvent;
