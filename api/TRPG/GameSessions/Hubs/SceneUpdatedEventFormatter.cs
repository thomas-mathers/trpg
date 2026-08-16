using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns.Events;
using TRPG.GameSessions.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class SceneUpdatedEventFormatter : GameClientEventFormatter<SceneUpdatedEvent>
{
    protected override GameClientMessage Format(SceneUpdatedEvent gameEvent) =>
        new(gameEvent.MethodName, gameEvent.Scene.ToSnapshot());
}
