using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Weather.Results;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Weather.Commands;

public class ApplyWeatherForecastsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<WeatherForecast> Forecasts { get; init; }
}

internal class ApplyWeatherForecastsCommandHandler(IWeatherDbContext context)
    : ICommandHandler<ApplyWeatherForecastsCommand>
{
    public async Task Handle(
        ApplyWeatherForecastsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stateIds = command.Forecasts.Select(forecast => forecast.StateId).ToArray();
        var states = await context
            .WeatherStates.Where(w => w.WorldId == command.WorldId && stateIds.Contains(w.StateId))
            .ToDictionaryAsync(w => w.StateId, cancellationToken);
        foreach (var forecast in command.Forecasts)
        {
            if (!states.TryGetValue(forecast.StateId, out var state))
            {
                continue;
            }

            state.Condition = forecast.Condition;
            state.NextChangeGameTime = forecast.NextChangeAt;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
