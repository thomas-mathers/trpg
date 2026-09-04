namespace TRPG.Application.Configuration;

public class FleeOptions
{
    public float CatchChanceMultiplier { get; init; } = 0.5f;
    public float MinimumCatchChance { get; init; } = 0.05f;
    public float MaximumCatchChance { get; init; } = 0.95f;
}
