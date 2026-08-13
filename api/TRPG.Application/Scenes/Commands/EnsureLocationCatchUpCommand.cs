using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

internal class EnsureLocationCatchUpCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required Guid SessionId { get; init; }
}

internal record EnsureLocationCatchUpResult(bool CatchUpRan, InGameDate CurrentDate);

internal class EnsureLocationCatchUpCommandHandler(
    GetPlaytimeQueryHandler getPlaytime,
    SyncCommandHandler sync,
    IMemoryCache cache,
    ILogger<EnsureLocationCatchUpCommandHandler> logger
)
{
    public async Task<EnsureLocationCatchUpResult> Handle(
        EnsureLocationCatchUpCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(playtime);

        var cacheKey = $"catchup:{command.WorldId}:{command.LocationId}:{currentDate.Hour}";

        if (cache.TryGetValue(cacheKey, out bool _))
        {
            logger.LogInformation("[perf] Catch-up cache hit for {CacheKey}", cacheKey);
            return new EnsureLocationCatchUpResult(false, currentDate);
        }

        logger.LogInformation(
            "[perf] Catch-up cache miss for {CacheKey}, running catch-up",
            cacheKey
        );

        await sync.Handle(
            new SyncCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
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

        return new EnsureLocationCatchUpResult(true, currentDate);
    }
}
