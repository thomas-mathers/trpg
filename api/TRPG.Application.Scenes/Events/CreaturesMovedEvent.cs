using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Events;

public record CreaturesMovedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlyDictionary<Guid, Placement> CreaturePlacements
) : GameClientEvent(WorldId);
