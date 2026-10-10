using TRPG.Application.Common.Events;

namespace TRPG.Application.Creatures.Events;

public record PlayerCorrectedEvent(
    Guid WorldId,
    Guid PlayerId,
    Guid LocationId,
    double OffsetX,
    double OffsetY
) : GameClientEvent(WorldId);
