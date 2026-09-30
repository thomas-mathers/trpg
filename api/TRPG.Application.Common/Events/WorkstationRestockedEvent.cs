using TRPG.Domain;

namespace TRPG.Application.Common.Events;

public sealed record WorkstationRestockedEvent(
    Guid WorldId,
    Guid BuildingId,
    Guid WorkstationId,
    GameInstant GameTime
) : DomainEvent;
