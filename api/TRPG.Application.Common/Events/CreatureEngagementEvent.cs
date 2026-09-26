using TRPG.Domain;

namespace TRPG.Application.Common.Events;

public sealed record CreaturesEngagedEvent(
    Guid WorldId,
    IReadOnlyCollection<Guid> CreatureIds,
    GameInstant GameTime
) : DomainEvent;

public sealed record CreaturesReleasedEvent(
    Guid WorldId,
    IReadOnlyCollection<Guid> CreatureIds,
    GameInstant GameTime
) : DomainEvent;
