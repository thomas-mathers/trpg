namespace TRPG.Application.Configuration;

public class CreatureRegenOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromSeconds(5);
    public float HpRegenPercentPerTick { get; init; } = 0.05f;
    public float ApRegenPercentPerTick { get; init; } = 0.10f;
    public float MpRegenPercentPerTick { get; init; } = 0.05f;
}
