namespace TRPG.Application.Configuration;

public class TrapOptions
{
    public int MaxTrapsPerDungeon { get; init; } = 2;
    public float BaseAttemptChance { get; init; } = 0.5f;
    public float AttemptChancePerDexterityPoint { get; init; } = 0.02f;
    public float MinimumAttemptChance { get; init; } = 0.05f;
    public float MaximumAttemptChance { get; init; } = 0.95f;
}
