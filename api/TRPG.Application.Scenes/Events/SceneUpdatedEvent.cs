using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Scenes.Events;

public record SceneUpdatedEvent(SceneResult Scene, WorldStateStamp Stamp) : GameClientEvent;
