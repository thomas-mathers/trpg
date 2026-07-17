using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneWithCatchUpQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid? RoomId { get; init; }
    public required Guid? DistrictId { get; init; }
    public required Guid StateId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class GetSceneWithCatchUpQueryHandler(
    SyncCommandHandler sync,
    GetSceneQueryHandler getScene,
    IMemoryCache cache,
    ILogger<GetSceneWithCatchUpQueryHandler> logger
)
{
    public async Task<SceneResult> Handle(
        GetSceneWithCatchUpQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var locationId = query.RoomId ?? query.DistrictId ?? query.StateId;
        var cacheKey = $"scene:{query.WorldId}:{locationId}:{query.CurrentDate.Hour}";

        if (cache.TryGetValue(cacheKey, out SceneResult? cachedScene))
        {
            logger.LogInformation("[perf] Scene cache hit for {CacheKey}", cacheKey);
            return cachedScene!;
        }

        logger.LogInformation("[perf] Scene cache miss for {CacheKey}, running catch-up", cacheKey);

        await sync.Handle(
            new SyncCommand
            {
                WorldId = query.WorldId,
                RoomId = query.RoomId,
                DistrictId = query.DistrictId,
                CurrentDate = query.CurrentDate,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
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
