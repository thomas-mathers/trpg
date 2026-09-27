using TRPG.Domain;

namespace TRPG.Application.Common.Events;

public sealed record PlayerMovedEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid FromLocationId,
    Guid ToLocationId,
    GameInstant GameTime
) : DomainEvent;
