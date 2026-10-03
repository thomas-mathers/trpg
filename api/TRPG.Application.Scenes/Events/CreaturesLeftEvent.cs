using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record CreaturesLeftEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlySet<Guid> CreatureIds
) : GameClientEvent(WorldId);
