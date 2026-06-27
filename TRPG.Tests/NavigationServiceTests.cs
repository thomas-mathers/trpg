using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class NavigationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private MemoryCache _cache = null!;
    private City _city = null!;
    private TrpgDbContext _context = null!;
    private Country _country = null!;
    private NavigationService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new NavigationService(_context, _cache);

        var world = Builders.MakeWorld();
        _country = Builders.MakeCountry(world.Id);
        _city = Builders.MakeCity(_country.Id);

        _context.Worlds.Add(world);
        _context.Countries.Add(_country);
        _context.Cities.Add(_city);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        _cache.Dispose();
        await _context.DisposeAsync();
    }

    private async Task<City> SeedCity() {
        var city = Builders.MakeCity(_country.Id);
        _context.Cities.Add(city);
        await _context.SaveChangesAsync();
        return city;
    }

    private async Task<Road> SeedRoad(Guid destinationCityId) {
        var road = new Road {
            OriginCityId = _city.Id,
            DestinationCityId = destinationCityId,
            Name = $"Road-{Guid.NewGuid():N}",
            Distance = 10f,
            TravelTime = 5,
            DangerLevel = 0.1f
        };
        _context.Roads.Add(road);
        await _context.SaveChangesAsync();
        return road;
    }

    private async Task<Building> SeedBuilding() {
        var building = Builders.MakeBuilding(_city.Id);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync();
        return building;
    }

    private async Task<BuildingRoom> SeedRoom(Guid buildingId) {
        var room = Builders.MakeBuildingRoom(buildingId);
        _context.BuildingRooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    [Fact]
    public async Task GetShortestCityRoute_ReturnsRoute_WhenDirectRouteExists() {
        // Arrange
        var cityB = await SeedCity();
        var road = await SeedRoad(cityB.Id);

        // Act
        var result = await _service.GetShortestCityRoute(_city.Id, cityB.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(road.Id, result[0].Id);
    }

    [Fact]
    public async Task GetShortestCityRoute_ReturnsEmpty_WhenNoRouteExists() {
        // Act
        var result =
            await _service.GetShortestCityRoute(_city.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShortestIntraCityRoute_ReturnsPath_WhenNoObstacles() {
        // Arrange
        var origin = new Point(0, 0);
        var destination = new Point(0, 2);

        // Act
        var result =
            await _service.GetShortestIntraCityRoute(_city.Id, origin, destination,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(origin, result[0]);
        Assert.Equal(destination, result[^1]);
    }

    [Fact]
    public async Task GetShortestIntraCityRoute_ThrowsInvalidOperationException_WhenCityNotFound() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetShortestIntraCityRoute(Guid.NewGuid(), new Point(0, 0), new Point(1, 0),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetShortestIntraRoomRoute_ReturnsPath_WhenNoObstacles() {
        // Arrange
        var building = await SeedBuilding();
        var room = await SeedRoom(building.Id);
        var origin = new Point(0, 0);
        var destination = new Point(2, 0);

        // Act
        var result = await _service.GetShortestIntraRoomRoute(room.Id, origin, destination,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(origin, result[0]);
        Assert.Equal(destination, result[^1]);
    }

    [Fact]
    public async Task GetShortestIntraRoomRoute_ThrowsInvalidOperationException_WhenRoomNotFound() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetShortestIntraRoomRoute(Guid.NewGuid(), new Point(0, 0), new Point(1, 0),
                TestContext.Current.CancellationToken));
    }
}