namespace TRPG.Domain.Models;

public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Snow,
    Fog,
}

public static class WeatherConditions
{
    public static bool PreventsOptionalTravel(WeatherCondition? condition) =>
        condition is WeatherCondition.Storm or WeatherCondition.Snow;
}

public class WeatherState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid StateId { get; init; }
    public WeatherCondition Condition { get; set; }
    public GameInstant NextChangeGameTime { get; set; } = GameClock.Epoch;
}
