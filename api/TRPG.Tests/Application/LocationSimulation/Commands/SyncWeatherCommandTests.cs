using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncWeatherCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncWeatherCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncWeatherCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<WeatherState> SeedWeatherState(Guid stateId, TimeSpan nextChangePlaytime)
    {
        var weatherState = new WeatherState
        {
            WorldId = WorldId,
            StateId = stateId,
            Condition = WeatherCondition.Clear,
            NextChangePlaytime = nextChangePlaytime,
        };
        _context.WeatherStates.Add(weatherState);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return weatherState;
    }

    [Fact]
    public async Task Handle_CreatesWeatherState_WhenNoneExistsForTheState()
    {
        // Arrange
        var stateId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new SyncWeatherCommand
            {
                WorldId = WorldId,
                StateId = stateId,
                CurrentPlaytime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var weatherState = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.True(weatherState.NextChangePlaytime > TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNotYetDue()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var farFuture = TimeSpan.FromDays(30);
        await SeedWeatherState(stateId, farFuture);

        // Act
        await _handler.Handle(
            new SyncWeatherCommand
            {
                WorldId = WorldId,
                StateId = stateId,
                CurrentPlaytime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var weatherState = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(WeatherCondition.Clear, weatherState.Condition);
        Assert.Equal(farFuture, weatherState.NextChangePlaytime);
    }

    [Fact]
    public async Task Handle_AdvancesTheNextChangePlaytime_WhenDue()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        await SeedWeatherState(stateId, TimeSpan.Zero);
        var currentPlaytime = TimeSpan.FromHours(1);

        // Act
        await _handler.Handle(
            new SyncWeatherCommand
            {
                WorldId = WorldId,
                StateId = stateId,
                CurrentPlaytime = currentPlaytime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var weatherState = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.True(weatherState.NextChangePlaytime > currentPlaytime);
    }
}
