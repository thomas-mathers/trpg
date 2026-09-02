using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Results;

namespace TRPG.Application.GameTurns.Events;

public record SceneUpdatedEvent(SceneResult Scene) : GameClientEvent;
