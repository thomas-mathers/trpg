using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record ClockReanchoredEvent(Guid WorldId, Guid LocationId, WorldStateStamp Stamp)
    : GameClientEvent(WorldId);
