using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.Scenes.Queries;

internal class GetCurrentSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class GetCurrentSceneQueryHandler(
    GetPlaytimeQueryHandler getPlaytime,
    GetSceneQueryHandler getScene
)
{
    public async Task<SceneResult> Handle(
        GetCurrentSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = query.SessionId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        return await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );
    }
}
