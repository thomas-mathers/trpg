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
        var cacheKey = $"catchup:{query.WorldId}:{locationId}:{query.CurrentDate.Hour}";

        // Only the NPC-schedule catch-up simulation is cached (it's idempotent for a given
        // location+hour and expensive to rerun) — the scene itself is always fetched live,
        // since HP/combat state can change within the same in-game hour and must never be stale.
        if (cache.TryGetValue(cacheKey, out bool _))
        {
            logger.LogInformation("[perf] Catch-up cache hit for {CacheKey}", cacheKey);
        }
        else
        {
            logger.LogInformation("[perf] Catch-up cache miss for {CacheKey}, running catch-up", cacheKey);

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

            cache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = GameClock.RealTimePerInGameHour,
                }
            );
        }

        return await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = query.CurrentDate,
            },
            cancellationToken
        );
    }
}
