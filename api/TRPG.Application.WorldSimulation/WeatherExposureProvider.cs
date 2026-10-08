using TRPG.Application.Common.Queries;
using TRPG.Application.Weather.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public sealed class WeatherExposureProvider(
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocations,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeather
)
{
    public async Task<IReadOnlySet<Guid>> FindExposedLocationIds(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken = default
    )
    {
        var locations = await getLocations.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );

        var exposed = new HashSet<Guid>();
        foreach (
            var stateLocations in locations
                .Values.Where(location => location.Kind != LocationKind.Room)
                .GroupBy(location => location.StateId)
        )
        {
            var weather = await getWeather.Handle(
                new GetWeatherByStateIdQuery { StateId = stateLocations.Key },
                cancellationToken
            );
            if (WeatherConditions.PreventsOptionalTravel(weather))
            {
                exposed.UnionWith(stateLocations.Select(location => location.Id));
            }
        }

        return exposed;
    }
}
