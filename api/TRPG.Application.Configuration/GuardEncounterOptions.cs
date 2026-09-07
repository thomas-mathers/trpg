namespace TRPG.Application.Configuration;

public class GuardEncounterOptions
{
    public int ReputationThreshold { get; init; } = -25;
    public float EncounterChance { get; init; } = 0.3f;

    // Rates are set so a sentence keeps growing across the whole reputation range down to -100.
    public float FineGoldPerReputationPoint { get; init; } = 2f;
    public int MinimumFineGold { get; init; } = 5;
    public int MaxFineGold { get; init; } = 250;
    public float JailHoursPerReputationPoint { get; init; } = 0.25f;
    public int MinimumJailHours { get; init; } = 1;
    public int MaxJailHours { get; init; } = 24;
}
