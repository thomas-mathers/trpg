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

public class WeatherState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid StateId { get; init; }
    public WeatherCondition Condition { get; set; }
    public TimeSpan NextChangePlaytime { get; set; }
}
