namespace TRPG.Application.Configuration;

public class GameClockOptions
{
    public TimeSpan RealTimePerMessage { get; init; } = TimeSpan.FromSeconds(54);
}
