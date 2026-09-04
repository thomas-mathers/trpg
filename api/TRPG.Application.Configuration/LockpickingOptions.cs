namespace TRPG.Application.Configuration;

public class LockpickingOptions
{
    public float BaseChance { get; init; } = 0.6f;
    public float ChancePerLockLevel { get; init; } = -0.15f;
    public float ChancePerSkillLevel { get; init; } = 0.05f;
    public float MinimumChance { get; init; } = 0.05f;
    public float MaximumChance { get; init; } = 0.95f;
    public float BaseDetectionChance { get; init; } = 0.5f;
    public float DetectionChanceReductionPerSkillLevel { get; init; } = 0.05f;
    public float MinimumDetectionChance { get; init; } = 0.05f;
    public float MaximumDetectionChance { get; init; } = 0.95f;
}
