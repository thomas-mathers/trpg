namespace TRPG.Application.Configuration;

public class ReputationOptions
{
    public int KillReputationPenalty { get; init; } = -100;
    public int ApologizedTheftReputationPenalty { get; init; } = -10;
    public int TheftReputationPenalty { get; init; } = -25;
    public int BreakingAndEnteringReputationPenalty { get; init; } = -20;
}
