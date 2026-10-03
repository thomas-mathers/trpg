using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record CreaturesArrivedEvent(
    Guid WorldId,
    Guid LocationId,
    WorldStateStamp Stamp,
    IReadOnlyCollection<SceneCreatureInfo> Creatures
) : GameClientEvent(WorldId);
