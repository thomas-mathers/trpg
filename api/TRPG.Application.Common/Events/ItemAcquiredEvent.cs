namespace TRPG.Application.Common.Events;

public sealed record ItemAcquiredEvent(Guid PlayerId, Guid WorldId, Guid ItemId) : DomainEvent;
