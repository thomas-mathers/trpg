using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.WorldSimulation.Events;

public sealed record PropOccupancy(Guid PropId, bool IsOccupied, bool IsOccupiedByPlayer);

public record PropOccupancyChangedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlyCollection<PropOccupancy> Props
) : GameClientEvent(WorldId), ILocationScopedEvent;
