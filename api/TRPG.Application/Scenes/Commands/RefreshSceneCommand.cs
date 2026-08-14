using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

internal class RefreshSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal record RefreshSceneResult(SceneResult Scene, bool Refreshed);

internal class RefreshSceneCommandHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    GetPlaytimeQueryHandler getPlaytime,
    SyncLocationCommandHandler syncLocation,
    GetSceneQueryHandler getScene,
    IMemoryCache cache,
    ILogger<RefreshSceneCommandHandler> logger
)
{
    public async Task<RefreshSceneResult> Handle(
        RefreshSceneCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        var refreshed = await RefreshLocation(
            command.WorldId,
            player!.LocationId,
            currentDate,
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        return new RefreshSceneResult(scene, refreshed);
    }

    private async Task<bool> RefreshLocation(
        Guid worldId,
        Guid locationId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var cacheKey = $"catchup:{worldId}:{locationId}:{currentDate.Hour}";

        if (cache.TryGetValue(cacheKey, out bool _))
        {
            logger.LogInformation("[perf] Catch-up cache hit for {CacheKey}", cacheKey);
            return false;
        }

        logger.LogInformation(
            "[perf] Catch-up cache miss for {CacheKey}, running catch-up",
            cacheKey
        );

        await syncLocation.Handle(
            new SyncLocationCommand
            {
                WorldId = worldId,
                LocationId = locationId,
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

        return true;
    }
}
