using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.GameSessions;

namespace TRPG.Application.Scenes;

public class SceneCatchUpCache(IMemoryCache cache)
{
    public bool HasCaughtUp(Guid worldId, Guid locationId, int hour) =>
        cache.TryGetValue(BuildKey(worldId, locationId, hour), out bool _);

    public void MarkCaughtUp(Guid worldId, Guid locationId, int hour) =>
        cache.Set(
            BuildKey(worldId, locationId, hour),
            true,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GameClock.RealTimePerInGameHour,
            }
        );

    public void Evict(Guid worldId, Guid locationId, int hour) =>
        cache.Remove(BuildKey(worldId, locationId, hour));

    private static string BuildKey(Guid worldId, Guid locationId, int hour) =>
        $"catchup:{worldId}:{locationId}:{hour}";
}
