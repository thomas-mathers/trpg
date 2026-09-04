namespace TRPG.Application.Configuration;

public class GuardEncounterOptions
{
    public int ReputationThreshold { get; init; } = -25;
    public float EncounterChance { get; init; } = 0.3f;
    public float FineGoldPerReputationPoint { get; init; } = 5f;
    public int MinimumFineGold { get; init; } = 5;
    public int MaxFineGold { get; init; } = 250;
    public float JailHoursPerReputationPoint { get; init; } = 0.5f;
    public int MinimumJailHours { get; init; } = 1;
    public int MaxJailHours { get; init; } = 24;
}
