using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Weather.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Weather.Commands;

public sealed class EnsureWeatherCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EnsureWeatherCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EnsureWeatherCommandHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesWeatherState_WhenNoneExistsForTheState()
    {
        // Arrange
        var stateId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new EnsureWeatherCommand
            {
                WorldId = WorldId,
                StateId = stateId,
                CurrentGameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var weatherState = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.True(weatherState.NextChangeGameTime > GameClock.Epoch);
    }

    [Fact]
    public async Task Handle_LeavesTheStateAlone_WhenOneExistsAndIsDue()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        _context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = WorldId,
                StateId = stateId,
                Condition = WeatherCondition.Clear,
                NextChangeGameTime = GameClock.Epoch,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new EnsureWeatherCommand
            {
                WorldId = WorldId,
                StateId = stateId,
                CurrentGameTime = GameClock.Epoch + TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var weatherState = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            (WeatherCondition.Clear, GameClock.Epoch),
            (weatherState.Condition, weatherState.NextChangeGameTime)
        );
    }
}
