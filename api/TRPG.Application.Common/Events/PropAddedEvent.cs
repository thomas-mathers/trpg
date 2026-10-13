namespace TRPG.Application.Common.Events;

public sealed record PropAddedEvent(Guid WorldId, Guid LocationId) : DomainEvent;
