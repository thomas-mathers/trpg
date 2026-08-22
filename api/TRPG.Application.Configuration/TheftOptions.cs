namespace TRPG.Application.Configuration;

public class TheftOptions
{
    public float BaseDetectionChance { get; init; } = 0.5f;
    public float MaximumDetectionChance { get; init; } = 0.95f;
    public float MinimumDetectionChance { get; init; } = 0.05f;
    public float DetectionChanceReductionPerSkillLevel { get; init; } = 0.05f;
    public float DetectionChanceIncreasePerItem { get; init; } = 0.05f;
}
