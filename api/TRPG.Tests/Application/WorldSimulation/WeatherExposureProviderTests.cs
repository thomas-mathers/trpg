using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldSimulation;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class WeatherExposureProviderTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WeatherExposureProvider _provider = null!;
    private State _state = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _provider = _serviceProvider.GetRequiredService<WeatherExposureProvider>();
        _state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        _context.States.Add(_state);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task FindExposedLocationIds_ReturnsOutdoorLocations_InAStorm()
    {
        // Arrange
        var street = await AddLocation(LocationKind.District);
        await SetWeather(WeatherCondition.Storm);

        // Act
        var exposed = await _provider.FindExposedLocationIds(
            [street.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([street.Id], exposed);
    }

    [Fact]
    public async Task FindExposedLocationIds_ExcludesRooms_InAStorm()
    {
        // Arrange
        var room = await AddLocation(LocationKind.Room);
        await SetWeather(WeatherCondition.Storm);

        // Act
        var exposed = await _provider.FindExposedLocationIds(
            [room.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(exposed);
    }

    [Fact]
    public async Task FindExposedLocationIds_ReturnsNothing_InClearWeather()
    {
        // Arrange
        var street = await AddLocation(LocationKind.District);
        await SetWeather(WeatherCondition.Clear);

        // Act
        var exposed = await _provider.FindExposedLocationIds(
            [street.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(exposed);
    }

    private async Task<Location> AddLocation(LocationKind kind)
    {
        var location = Builders.MakeLocation(_worldId, _state.Id, kind: kind);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return location;
    }

    private async Task SetWeather(WeatherCondition condition)
    {
        _context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = _worldId,
                StateId = _state.Id,
                Condition = condition,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
