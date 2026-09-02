using TRPG.Application.Common.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain;

namespace TRPG.Application.GameTurns.Queries;

public class GetCurrentSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class GetCurrentSceneQueryHandler(IQueryHandler<GetSceneQuery, SceneResult> getScene)
    : IQueryHandler<GetCurrentSceneQuery, SceneResult>
{
    public async Task<SceneResult> Handle(
        GetCurrentSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var currentDate = GameClock.GetCurrentInGameDate(query.Playtime);

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
