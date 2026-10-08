namespace TRPG.Application.Common.Events;

public sealed record CreaturesSpawnedEvent(Guid WorldId, IReadOnlyCollection<Guid> CreatureIds)
    : DomainEvent;
