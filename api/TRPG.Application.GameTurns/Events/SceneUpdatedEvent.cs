using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.GameTurns.Events;

public record SceneUpdatedEvent(SceneResult Scene, WorldStateStamp Stamp) : GameClientEvent;
