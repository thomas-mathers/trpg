using TRPG.Application.Weather;
using TRPG.Application.Weather.Results;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Climate;

internal static class WeatherConcern
{
    public static IReadOnlyList<SimEvent> Tick(
        IReadOnlyCollection<WeatherForecast> forecasts,
        GameInstant now
    ) =>
        [
            .. forecasts
                .Select(forecast => WeatherTransition.Advance(forecast, now))
                .OfType<WeatherForecast>()
                .Select(advanced => new WeatherChanged(now, advanced)),
        ];
}
