using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Queries;

public class GetWeatherByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetWeatherByLocationIdQueryHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId
) : IQueryHandler<GetWeatherByLocationIdQuery, WeatherCondition?>
{
    public async Task<WeatherCondition?> Handle(
        GetWeatherByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = query.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return null;
        }

        return await getWeatherByStateId.Handle(
            new GetWeatherByStateIdQuery { StateId = location.StateId },
            cancellationToken
        );
    }
}
