using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.LocationSimulation;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.LocationSimulation;

public class LocationCatchUpCacheTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private static InGameDate MakeDate(int day, int hour) =>
        new(975, "Thawmoon", day, "Stormday", DayOfWeek.Thursday, hour);

    private static LocationCatchUpCache MakeCache() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void TryClaim_ReturnsFalse_WhenAlreadyClaimedForTheSameDateAndHour()
    {
        // Arrange
        var cache = MakeCache();
        var date = MakeDate(day: 1, hour: 8);
        cache.TryClaim(WorldId, LocationId, date);

        // Act
        var claimed = cache.TryClaim(WorldId, LocationId, date);

        // Assert
        Assert.False(claimed);
    }

    [Fact]
    public void TryClaim_ReturnsTrue_WhenTheSameHourRecursOnADifferentDay()
    {
        // Arrange — a naive hour-only key would collide here even though a full in-game day passed.
        var cache = MakeCache();
        cache.TryClaim(WorldId, LocationId, MakeDate(day: 1, hour: 8));

        // Act
        var claimed = cache.TryClaim(WorldId, LocationId, MakeDate(day: 2, hour: 8));

        // Assert
        Assert.True(claimed);
    }

    [Fact]
    public void Evict_AllowsTheLocationToBeClaimedAgain()
    {
        // Arrange
        var cache = MakeCache();
        var date = MakeDate(day: 1, hour: 8);
        cache.TryClaim(WorldId, LocationId, date);
        cache.Evict(WorldId, LocationId, date);

        // Act
        var claimed = cache.TryClaim(WorldId, LocationId, date);

        // Assert
        Assert.True(claimed);
    }
}
