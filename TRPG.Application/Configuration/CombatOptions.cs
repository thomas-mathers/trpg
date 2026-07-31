namespace TRPG.Application.Configuration;

public class CombatOptions
{
    public float ApRegenPercentPerRound { get; init; } = 0.25f;
    public float MpRegenPercentPerRound { get; init; } = 0.10f;
    public int BaseProficiency { get; init; } = 50;
    public float NonPlayerProficiencyBase { get; init; } = 113f;
    public float NonPlayerProficiencyPerLevel { get; init; } = 4.7f;
    public float MinHitChance { get; init; } = 0.05f;
    public float MaxHitChance { get; init; } = 0.95f;
    public float StrengthDamageBonusPerPoint { get; init; } = 0.01f;
    public float IntelligenceDamageLogDivisor { get; init; } = 50f;
    public float MaxResistancePercent { get; init; } = 0.75f;
    public float EvasionPerDexterityPoint { get; init; } = 0.6f;
    public float LowResourceThresholdPercent { get; init; } = 0.3f;
    public float OpeningBuffChancePercent { get; init; } = 0.5f;
    public float CritChancePerDexterityPoint { get; init; } = 0.002f;
    public float MaxCritChance { get; init; } = 0.5f;
    public float CritDamageMultiplier { get; init; } = 2.5f;
}
