namespace TRPG.Application.Common.Events;

public sealed record CreaturesDiedEvent(Guid WorldId, IReadOnlyCollection<Guid> CreatureIds)
    : DomainEvent;
