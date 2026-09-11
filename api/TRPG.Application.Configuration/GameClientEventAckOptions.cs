namespace TRPG.Application.Configuration;

public class GameClientEventAckOptions
{
    public TimeSpan AckTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
