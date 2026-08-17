using TRPG.Application.GameTurns.Events;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class SceneUpdatedEventMapper : GameClientEventMapper<SceneUpdatedEvent>
{
    protected override IGameClientCall Map(SceneUpdatedEvent gameEvent) =>
        new GameClientCall<SceneSnapshot>(
            gameEvent.Scene.ToSnapshot(),
            static (client, arguments) => client.SceneSnapshot(arguments)
        );
}
