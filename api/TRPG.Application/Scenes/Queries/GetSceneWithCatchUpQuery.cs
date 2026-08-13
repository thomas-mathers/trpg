using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Commands;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneWithCatchUpQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class GetSceneWithCatchUpQueryHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    GetPlaytimeQueryHandler getPlaytime,
    SyncCommandHandler sync,
    GetSceneQueryHandler getScene,
    IMemoryCache cache,
    IGameClientEventSink gameEvents,
    ILogger<GetSceneWithCatchUpQueryHandler> logger
)
{
    public async Task<SceneResult?> Handle(
        GetSceneWithCatchUpQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.PlayerId },
            cancellationToken
        );

        if (player == null)
        {
            return null;
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = query.SessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        var cacheKey = $"catchup:{query.WorldId}:{player.LocationId}:{currentDate.Hour}";

        var catchUpRan = false;

        if (cache.TryGetValue(cacheKey, out bool _))
        {
            logger.LogInformation("[perf] Catch-up cache hit for {CacheKey}", cacheKey);
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
                    LocationId = player.LocationId,
                    CurrentDate = currentDate,
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
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        if (catchUpRan)
        {
            gameEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(scene),
                    SceneUpdateReason.CatchUp
                )
            );
        }

        return scene;
    }
}
