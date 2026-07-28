namespace TRPG.Application.Configuration;

public class CombatOptions
{
    public float ApRegenPercentPerRound { get; init; } = 0.25f;
    public float MpRegenPercentPerRound { get; init; } = 0.10f;
    public int BaseProficiency { get; init; } = 50;
    public int UnarmedBaseDamage { get; init; } = 3;
    public float MinHitChance { get; init; } = 0.05f;
    public float MaxHitChance { get; init; } = 0.95f;
    public float StrengthDamageBonusPerPoint { get; init; } = 0.01f;
    public float IntelligenceDamageBonusPerPoint { get; init; } = 0.01f;
    public float LowResourceThresholdPercent { get; init; } = 0.3f;
}
