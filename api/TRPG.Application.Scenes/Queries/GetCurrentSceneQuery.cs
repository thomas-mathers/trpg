using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Domain;

namespace TRPG.Application.Scenes.Queries;

public class GetCurrentSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class GetCurrentSceneQueryHandler(IQueryHandler<GetSceneQuery, SceneResult> getScene)
    : IQueryHandler<GetCurrentSceneQuery, SceneResult>
{
    public async Task<SceneResult> Handle(
        GetCurrentSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var currentDate = GameClock.GetCurrentInGameDate(query.GameTime);

        return await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = currentDate,
                GameTime = query.GameTime,
            },
            cancellationToken
        );
    }
}
