using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneWithCatchUpQuery
{
    public required GameSession Session { get; init; }
    public required Guid? RoomId { get; init; }
    public required Guid? DistrictId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class GetSceneWithCatchUpQueryHandler(
    SyncCommandHandler sync,
    GetSceneQueryHandler getScene,
    IMemoryCache cache
)
{
    public async Task<SceneResult> Handle(
        GetSceneWithCatchUpQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var locationId = query.RoomId ?? query.DistrictId;
        var cacheKey = $"scene:{query.Session.WorldId}:{locationId}:{query.CurrentDate.Hour}";

        if (cache.TryGetValue(cacheKey, out SceneResult? cachedScene))
        {
            return cachedScene!;
        }

        await sync.Handle(
            new SyncCommand
            {
                WorldId = query.Session.WorldId,
                RoomId = query.RoomId,
                DistrictId = query.DistrictId,
                CurrentDate = query.CurrentDate,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.Session.WorldId,
                PlayerId = query.Session.PlayerId,
                CurrentDate = query.CurrentDate,
            },
            cancellationToken
        );

        cache.Set(
            cacheKey,
            scene,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GameClock.RealTimePerInGameHour,
            }
        );

        return scene;
    }
}
