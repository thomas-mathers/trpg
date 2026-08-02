namespace TRPG.Application.Configuration;

public class GameSessionOptions
{
    // Comfortably longer than the web client's own reconnect window (useGameHubConnection.ts:
    // RECONNECTION_MAX_ATTEMPTS=3 * RECONNECTION_DELAY_MS=5000, ~15-20s including attempt
    // latency) so a legitimate reconnect always wins the race against this grace period.
    public TimeSpan SessionEndGracePeriod { get; init; } = TimeSpan.FromSeconds(30);
}
