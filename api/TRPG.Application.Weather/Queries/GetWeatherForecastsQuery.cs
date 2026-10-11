using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Weather.Results;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Weather.Queries;

public class GetWeatherForecastsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetWeatherForecastsQueryHandler(IWeatherDbContext context)
    : IQueryHandler<GetWeatherForecastsQuery, IReadOnlyCollection<WeatherForecast>>
{
    public async Task<IReadOnlyCollection<WeatherForecast>> Handle(
        GetWeatherForecastsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .WeatherStates.Where(w => w.WorldId == query.WorldId)
            .Select(w => new WeatherForecast(w.StateId, w.Condition, w.NextChangeGameTime))
            .ToArrayAsync(cancellationToken);
}
