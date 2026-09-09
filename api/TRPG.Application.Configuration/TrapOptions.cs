namespace TRPG.Application.Configuration;

public class TrapOptions
{
    public int MaxTrapsPerDungeon { get; init; } = 2;
    public float BaseNoticeChance { get; init; } = 0.35f;
    public float SneakingNoticeBonus { get; init; } = 0.4f;
    public float MinimumNoticeChance { get; init; } = 0.05f;
    public float MaximumNoticeChance { get; init; } = 0.95f;
    public float BaseAttemptChance { get; init; } = 0.5f;
    public float AttemptChancePerDexterityPoint { get; init; } = 0.02f;
    public float MinimumAttemptChance { get; init; } = 0.05f;
    public float MaximumAttemptChance { get; init; } = 0.95f;
}
