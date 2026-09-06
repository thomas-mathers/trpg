namespace TRPG.Application.Configuration;

public class ReputationOptions
{
    public int KillReputationPenalty { get; init; } = -100;
    public int ApologizedTheftReputationPenalty { get; init; } = -10;
    public int TheftReputationPenalty { get; init; } = -25;
    public int LockpickingReputationPenalty { get; init; } = -10;
    public int SettledLockpickingReputationPenalty { get; init; } = -4;
    public int TrespassingReputationPenalty { get; init; } = -10;
    public int JailbreakReputationPenalty { get; init; } = -50;
    public int SettledJailbreakReputationPenalty { get; init; } = -20;
}
