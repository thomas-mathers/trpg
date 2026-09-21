using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Queries;

public class GetWeatherByStateIdQuery
{
    public required Guid StateId { get; init; }
}

internal class GetWeatherByStateIdQueryHandler(ILocationSimulationDbContext context)
    : IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?>
{
    public async Task<WeatherCondition?> Handle(
        GetWeatherByStateIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .WeatherStates.Where(w => w.StateId == query.StateId)
            .Select(w => (WeatherCondition?)w.Condition)
            .FirstOrDefaultAsync(cancellationToken);
}
