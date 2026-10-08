using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Commands;

public sealed class SyncLocationRoutinesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncLocationRoutinesCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncLocationRoutinesCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheLocationDoesNotExist()
    {
        // Act
        var exception = await Record.ExceptionAsync(() =>
            _handler.Handle(
                new SyncLocationRoutinesCommand
                {
                    WorldId = WorldId,
                    PlayerId = PlayerId,
                    LocationId = Guid.NewGuid(),
                    PlayerLevel = 1,
                    GameTime = GameClock.Epoch,
                },
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_KeepsTheWeatherSchedule_WhenRunAgainAtTheSameInstant()
    {
        // Arrange
        var location = await SeedLocation();
        await SyncAt(location, GameClock.Epoch);
        var weatherAfterFirstRun = await LoadWeather(location.StateId);

        // Act
        await _handler.Handle(
            new SyncLocationRoutinesCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                LocationId = location.Id,
                PlayerLevel = 1,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var weatherAfterSecondRun = await LoadWeather(location.StateId);
        Assert.Equal(weatherAfterFirstRun.Condition, weatherAfterSecondRun.Condition);
        Assert.Equal(
            weatherAfterFirstRun.NextChangeGameTime,
            weatherAfterSecondRun.NextChangeGameTime
        );
    }

    private async Task<Location> SeedLocation()
    {
        var location = Builders.MakeLocation(WorldId, kind: LocationKind.Wilderness);
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return location;
    }

    private async Task<Creature> SeedSleeper(Location location)
    {
        var sleeper = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Creatures.Add(sleeper);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                sleeper.Id,
                action: CreatureJobAction.Sleep,
                startHour: 6,
                endHour: 22,
                locationId: location.Id,
                worldId: WorldId,
                priority: 100
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sleeper;
    }

    private Task SyncAt(Location location, GameInstant gameTime) =>
        _handler.Handle(
            new SyncLocationRoutinesCommand
            {
                WorldId = WorldId,
                PlayerId = PlayerId,
                LocationId = location.Id,
                PlayerLevel = 1,
                GameTime = gameTime,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<WeatherState> LoadWeather(Guid stateId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.WeatherStates.SingleAsync(
            weather => weather.StateId == stateId,
            TestContext.Current.CancellationToken
        );
    }
}
