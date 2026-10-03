using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record CaravansUpdatedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlyCollection<SceneCaravanInfo> Caravans
) : GameClientEvent(WorldId);
