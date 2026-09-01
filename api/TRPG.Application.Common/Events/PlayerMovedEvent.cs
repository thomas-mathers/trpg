namespace TRPG.Application.Common.Events;

public sealed record PlayerMovedEvent(Guid PlayerId, Guid WorldId, Guid LocationId) : DomainEvent;
