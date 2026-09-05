namespace TRPG.Application.Configuration;

public class SuspicionOptions
{
    public float BaseDetectionChance { get; init; } = 0.5f;
    public float MinimumDetectionChance { get; init; } = 0.05f;
    public float MaximumDetectionChance { get; init; } = 0.95f;
    public float DetectionChanceReductionPerSkillLevel { get; init; } = 0.05f;
    public int ComplyReputationPenalty { get; init; } = 2;
    public int FleeFailedReputationPenalty { get; init; } = 5;
}
