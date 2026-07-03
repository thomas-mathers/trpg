using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class NavigationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private MemoryCache _cache = null!;
    private State _state = null!;
    private TrpgDbContext _context = null!;
    private Country _country = null!;
    private NavigationService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new NavigationService(_context, _cache);

        var world = Builders.MakeWorld();
        _country = Builders.MakeCountry(world.Id);
        _state = Builders.MakeState(_country.Id);

        _context.Worlds.Add(world);
        _context.Countries.Add(_country);
        _context.States.Add(_state);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        _cache.Dispose();
        await _context.DisposeAsync();
    }

    private async Task<State> SeedState() {
        var state = Builders.MakeState(_country.Id);
        _context.States.Add(state);
        await _context.SaveChangesAsync();
        return state;
    }

    private async Task<Road> SeedRoad(Guid destinationStateId) {
        var road = new Road {
            OriginStateId = _state.Id,
            DestinationStateId = destinationStateId,
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
    public async Task GetShortestStateRoute_ReturnsRoute_WhenDirectRouteExists() {
        // Arrange
        var stateB = await SeedState();
        var road = await SeedRoad(stateB.Id);

        // Act
        var result = await _service.GetShortestStateRoute(_state.Id, stateB.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(road.Id, result.First().Id);
    }

    [Fact]
    public async Task GetShortestStateRoute_ReturnsEmpty_WhenNoRouteExists() {
        // Act
        var result =
            await _service.GetShortestStateRoute(_state.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

}
