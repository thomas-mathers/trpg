namespace TRPG.Application.Configuration;

public class ReputationOptions
{
    // Scaled against GuardEncounterOptions.ReputationThreshold: how many offences draw a guard.
    public int KillReputationPenalty { get; init; } = -35;
    public int AssaultReputationPenalty { get; init; } = -15;
    public int ApologizedTheftReputationPenalty { get; init; } = -3;
    public int TheftReputationPenalty { get; init; } = -9;
    public int LockpickingReputationPenalty { get; init; } = -5;
    public int SettledLockpickingReputationPenalty { get; init; } = -2;
    public int TrespassingReputationPenalty { get; init; } = -3;
    public int JailbreakReputationPenalty { get; init; } = -20;
    public int SettledJailbreakReputationPenalty { get; init; } = -8;

    // A crime lands hardest on whoever it was done to and lightest on the faction behind them.
    public double WitnessPenaltyMultiplier { get; init; } = 1.5;
    public double VictimPenaltyMultiplier { get; init; } = 2.0;
}
