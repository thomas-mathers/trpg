using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Weather.Commands;
using TRPG.Application.Weather.Results;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Weather.Commands;

public sealed class ApplyWeatherForecastsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<ApplyWeatherForecastsCommand> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<ApplyWeatherForecastsCommand>
        >();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesTheConditionAndNextChange_WhenTheStateExists()
    {
        // Arrange
        var stateId = await SeedState(WeatherCondition.Clear);
        var nextChangeAt = GameClock.Epoch + TimeSpan.FromHours(9);

        // Act
        await _handler.Handle(
            new ApplyWeatherForecastsCommand
            {
                WorldId = _worldId,
                Forecasts = [new WeatherForecast(stateId, WeatherCondition.Storm, nextChangeAt)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext.WeatherStates.SingleAsync(
            w => w.StateId == stateId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            (WeatherCondition.Storm, nextChangeAt),
            (stored.Condition, stored.NextChangeGameTime)
        );
    }

    [Fact]
    public async Task Handle_IgnoresAForecast_WhenNoStateExistsForIt()
    {
        // Arrange
        var stateId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new ApplyWeatherForecastsCommand
            {
                WorldId = _worldId,
                Forecasts = [new WeatherForecast(stateId, WeatherCondition.Fog, GameClock.Epoch)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.WeatherStates.AnyAsync(
                w => w.StateId == stateId,
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<Guid> SeedState(WeatherCondition condition)
    {
        var stateId = Guid.NewGuid();
        _context.WeatherStates.Add(
            new WeatherState
            {
                WorldId = _worldId,
                StateId = stateId,
                Condition = condition,
                NextChangeGameTime = GameClock.Epoch,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return stateId;
    }
}
