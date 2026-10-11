using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Events;

public record SeatingChangedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    Guid CreatureId,
    Guid? SeatId,
    CreaturePosture Posture,
    Placement Placement
) : GameClientEvent(WorldId), ILocationScopedEvent;
