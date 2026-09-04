namespace TRPG.Application.Configuration;

public class SneakOptions
{
    public float BaseHostileDetectionChance { get; init; } = 0.5f;
    public float HostileDetectionChanceReductionPerSkillLevel { get; init; } = 0.05f;
    public float MinimumHostileDetectionChance { get; init; } = 0.05f;
    public float MaximumHostileDetectionChance { get; init; } = 0.95f;
}
