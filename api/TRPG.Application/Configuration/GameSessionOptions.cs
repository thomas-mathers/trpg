namespace TRPG.Application.Configuration;

public class GameSessionOptions
{
    public TimeSpan SessionEndGracePeriod { get; init; } = TimeSpan.FromSeconds(30);
}
