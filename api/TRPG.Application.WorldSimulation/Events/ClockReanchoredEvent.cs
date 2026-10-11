using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.WorldSimulation.Events;

public record ClockReanchoredEvent(Guid WorldId, Guid LocationId, WorldStateStamp Stamp)
    : GameClientEvent(WorldId),
        ILocationScopedEvent;
