namespace TRPG.Application.Common.Events;

public sealed record CreaturesStartedWalkingEvent(IReadOnlyCollection<Guid> CreatureIds)
    : DomainEvent;
