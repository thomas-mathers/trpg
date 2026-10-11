namespace TRPG.Application.Common.Events;

public sealed record DoorLocksChangedEvent(Guid WorldId, IReadOnlyCollection<Guid> LocationIds)
    : DomainEvent;
