using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;

namespace TRPG.Application.GameTurns;

internal class TurnSceneDiffer(
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ScenePublisher scenePublisher,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState
)
{
    public async Task<SceneResult> Capture(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        return await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );
    }

    public async Task<SceneResult> EnqueueChange(
        SceneResult before,
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        // Stamped before the scene is read so a later-numbered snapshot never describes older state.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = session.WorldId },
            cancellationToken
        );
        var after = await Capture(session, cancellationToken);

        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        {
            scenePublisher.PublishIfChanged(session.WorldId, session.PlayerId, after, stamp);
        }

        return after;
    }
}
