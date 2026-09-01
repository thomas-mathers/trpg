using TRPG.Domain.Models;

namespace TRPG.Application.Common.Events;

public sealed record CreatureKilledEvent(
    Guid PlayerId,
    Guid WorldId,
    Guid CreatureId,
    CreatureType CreatureType
) : DomainEvent;
