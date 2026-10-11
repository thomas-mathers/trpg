using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Domain;

namespace TRPG.Application.GameTurns;

internal class TurnSceneReader(
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
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
}
