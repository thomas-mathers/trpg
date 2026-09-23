namespace TRPG.Application.Configuration;

public class IntimidationOptions
{
    public float SuccessChanceMultiplier { get; init; } = 0.5f;
    public float MinimumSuccessChance { get; init; } = 0.05f;
    public float MaximumSuccessChance { get; init; } = 0.95f;
}
