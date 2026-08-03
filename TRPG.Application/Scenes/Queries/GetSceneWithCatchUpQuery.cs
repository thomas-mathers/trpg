using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneWithCatchUpQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid? LocationId { get; init; }
    public required Guid StateId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class GetSceneWithCatchUpQueryHandler(
    SyncCommandHandler sync,
    GetSceneQueryHandler getScene,
    IMemoryCache cache,
    GameTurnContext turnContext,
    ILogger<GetSceneWithCatchUpQueryHandler> logger
)
{
    public async Task<SceneResult> Handle(
        GetSceneWithCatchUpQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var cacheKeyId = query.LocationId ?? query.StateId;
        var cacheKey = $"catchup:{query.WorldId}:{cacheKeyId}:{query.CurrentDate.Hour}";

        var catchUpRan = false;
        if (cache.TryGetValue(cacheKey, out bool _))
        {
            logger.LogInformation("[perf] Catch-up cache hit for {CacheKey}", cacheKey);
        }
        else if (query.LocationId == null)
        {
            logger.LogInformation(
                "[perf] Catch-up skipped for {CacheKey} — player has no location yet",
                cacheKey
            );
        }
        else
        {
            logger.LogInformation(
                "[perf] Catch-up cache miss for {CacheKey}, running catch-up",
                cacheKey
            );

            await sync.Handle(
                new SyncCommand
                {
                    WorldId = query.WorldId,
                    LocationId = query.LocationId.Value,
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

            catchUpRan = true;
        }

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = query.CurrentDate,
            },
            cancellationToken
        );

        if (catchUpRan)
        {
            turnContext.PendingEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(scene),
                    SceneUpdateReason.CatchUp
                )
            );
        }

        return scene;
    }
}
