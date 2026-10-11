using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Queries;
using TRPG.Application.Weather.Queries;
using TRPG.Application.Weather.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Climate;

internal sealed class WeatherPass(
    Guid worldId,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
)
{
    public async Task<IReadOnlyList<SimEvent>> Run(
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var forecasts = await scope
                .ServiceProvider.GetRequiredService<
                    IQueryHandler<GetWeatherForecastsQuery, IReadOnlyCollection<WeatherForecast>>
                >()
                .Handle(new GetWeatherForecastsQuery { WorldId = worldId }, cancellationToken);

            return WeatherConcern.Tick(forecasts, now);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Weather pass failed in world {WorldId}; retrying when next due",
                worldId
            );

            return [];
        }
    }
}
