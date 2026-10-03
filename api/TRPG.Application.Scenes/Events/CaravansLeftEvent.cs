using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record CaravansLeftEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlySet<Guid> CaravanIds
) : GameClientEvent(WorldId);
