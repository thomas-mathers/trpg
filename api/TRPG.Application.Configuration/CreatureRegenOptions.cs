namespace TRPG.Application.Configuration;

public class CreatureRegenOptions
{
    public float HpRegenPercentPerHour { get; init; } = 0.05f;
    public float ApRegenPercentPerHour { get; init; } = 0.10f;
    public float MpRegenPercentPerHour { get; init; } = 0.05f;
}
