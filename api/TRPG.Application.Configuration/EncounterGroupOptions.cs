namespace TRPG.Application.Configuration;

public class EncounterGroupOptions
{
    // Measured in game time; a faction that just confronted the player stays quiet this long at that location.
    public TimeSpan RepeatGracePeriod { get; init; } = TimeSpan.FromHours(2);
}
