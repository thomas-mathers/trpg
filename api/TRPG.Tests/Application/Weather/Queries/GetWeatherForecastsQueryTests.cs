using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Queries;
using TRPG.Application.Weather.Queries;
using TRPG.Application.Weather.Results;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Weather.Queries;

public sealed class GetWeatherForecastsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IQueryHandler<GetWeatherForecastsQuery, IReadOnlyCollection<WeatherForecast>> _handler =
        null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            IQueryHandler<GetWeatherForecastsQuery, IReadOnlyCollection<WeatherForecast>>
        >();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheWorldsForecasts()
    {
        // Arrange
        var ownStateId = Guid.NewGuid();
        var nextChangeAt = GameClock.Epoch + TimeSpan.FromHours(6);
        _context.WeatherStates.AddRange(
            MakeState(_worldId, ownStateId, WeatherCondition.Snow, nextChangeAt),
            MakeState(Guid.NewGuid(), Guid.NewGuid(), WeatherCondition.Rain, GameClock.Epoch)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var forecasts = await _handler.Handle(
            new GetWeatherForecastsQuery { WorldId = _worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new WeatherForecast(ownStateId, WeatherCondition.Snow, nextChangeAt),
            Assert.Single(forecasts)
        );
    }

    private static WeatherState MakeState(
        Guid worldId,
        Guid stateId,
        WeatherCondition condition,
        GameInstant nextChangeAt
    ) =>
        new()
        {
            WorldId = worldId,
            StateId = stateId,
            Condition = condition,
            NextChangeGameTime = nextChangeAt,
        };
}
