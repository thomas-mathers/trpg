using TRPG.Domain;

namespace TRPG.Application.Common.Events;

public sealed record CreatureFreedEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    GameInstant GameTime
) : DomainEvent;
