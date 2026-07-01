using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class NavigationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private MemoryCache _cache = null!;
    private Region _region = null!;
    private TrpgDbContext _context = null!;
    private Country _country = null!;
    private NavigationService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new NavigationService(_context, _cache);

        var world = Builders.MakeWorld();
        _country = Builders.MakeCountry(world.Id);
        _region = Builders.MakeRegion(_country.Id);

        _context.Worlds.Add(world);
        _context.Countries.Add(_country);
        _context.Regions.Add(_region);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        _cache.Dispose();
        await _context.DisposeAsync();
    }

    private async Task<Region> SeedRegion() {
        var region = Builders.MakeRegion(_country.Id);
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();
        return region;
    }

    private async Task<Road> SeedRoad(Guid destinationRegionId) {
        var road = new Road {
            OriginRegionId = _region.Id,
            DestinationRegionId = destinationRegionId,
            Name = $"Road-{Guid.NewGuid():N}",
            Distance = 10f,
            TravelTime = 5,
            DangerLevel = 0.1f
        };
        _context.Roads.Add(road);
        await _context.SaveChangesAsync();
        return road;
    }

    [Fact]
    public async Task GetShortestRegionRoute_ReturnsRoute_WhenDirectRouteExists() {
        // Arrange
        var regionB = await SeedRegion();
        var road = await SeedRoad(regionB.Id);

        // Act
        var result = await _service.GetShortestRegionRoute(_region.Id, regionB.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(road.Id, result.First().Id);
    }

    [Fact]
    public async Task GetShortestRegionRoute_ReturnsEmpty_WhenNoRouteExists() {
        // Act
        var result =
            await _service.GetShortestRegionRoute(_region.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShortestIntraRegionRoute_ReturnsPath_WhenNoObstacles() {
        // Arrange
        var origin = new Point(0, 0);
        var destination = new Point(0, 2);

        // Act
        var result =
            await _service.GetShortestIntraRegionRoute(_region.Id, origin, destination,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(origin, result.First());
        Assert.Equal(destination, result.Last());
    }

    [Fact]
    public async Task GetShortestIntraRegionRoute_ThrowsInvalidOperationException_WhenRegionNotFound() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetShortestIntraRegionRoute(Guid.NewGuid(), new Point(0, 0), new Point(1, 0),
                TestContext.Current.CancellationToken));
    }
}
