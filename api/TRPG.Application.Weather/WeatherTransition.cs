using TRPG.Application.Weather.Results;
using TRPG.Domain;

namespace TRPG.Application.Weather;

public static class WeatherTransition
{
    public static WeatherForecast? Advance(WeatherForecast current, GameInstant now)
    {
        if (now < current.NextChangeAt)
        {
            return null;
        }

        var season = GameClock.GetCurrentSeason(now);

        return current with
        {
            Condition = WeatherRoll.NextCondition(current.Condition, season),
            NextChangeAt = now + WeatherRoll.NextChangeOffset(),
        };
    }
}
