using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record SceneUpdatedEvent(Guid WorldId, SceneResult Scene, WorldStateStamp Stamp)
    : GameClientEvent(WorldId);
