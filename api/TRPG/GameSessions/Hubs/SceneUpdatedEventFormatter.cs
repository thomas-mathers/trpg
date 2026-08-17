using TRPG.Application.GameTurns.Events;
using TRPG.GameSessions.Mappers;

namespace TRPG.GameSessions.Hubs;

internal sealed class SceneUpdatedEventFormatter : GameClientEventFormatter<SceneUpdatedEvent>
{
    protected override Task Dispatch(IGameClient client, SceneUpdatedEvent gameEvent) =>
        client.SceneSnapshot(gameEvent.Scene.ToSnapshot());
}
