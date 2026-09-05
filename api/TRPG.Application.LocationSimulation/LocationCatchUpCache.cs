using Microsoft.Extensions.Caching.Memory;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation;

internal class LocationCatchUpCache(IMemoryCache cache)
{
    private readonly object _gate = new();

    public bool TryClaim(Guid worldId, Guid locationId, InGameDate currentDate)
    {
        var key = BuildKey(worldId, locationId, currentDate);

        lock (_gate)
        {
            if (cache.TryGetValue(key, out bool _))
            {
                return false;
            }

            cache.Set(
                key,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = GameClock.RealTimePerInGameHour,
                }
            );
            return true;
        }
    }

    public void Evict(Guid worldId, Guid locationId, InGameDate currentDate) =>
        cache.Remove(BuildKey(worldId, locationId, currentDate));

    private static string BuildKey(Guid worldId, Guid locationId, InGameDate currentDate) =>
        $"catchup:{worldId}:{locationId}:{currentDate.Year}:{currentDate.MonthName}:{currentDate.Day}:{currentDate.Hour}";
}
