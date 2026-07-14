namespace TRPG.Application.Common;

public enum LlmProvider
{
    Ollama,
    Anthropic,
}

public class LlmRoleOptions
{
    public LlmProvider Provider { get; init; } = LlmProvider.Ollama;
    public string Model { get; init; } = "llama3.1:8b";
    public bool? Think { get; init; }
    public float? Temperature { get; init; }
}

public class CombatOptions
{
    public float ApRegenPercentPerRound { get; init; } = 0.25f;
    public float MpRegenPercentPerRound { get; init; } = 0.10f;
    public int XpPerEnemyLevel { get; init; } = 25;
    public int BaseProficiency { get; init; } = 50;
    public int UnarmedBaseDamage { get; init; } = 3;
    public float MinHitChance { get; init; } = 0.05f;
    public float MaxHitChance { get; init; } = 0.95f;
    public float StrengthDamageBonusPerPoint { get; init; } = 0.01f;
    public float IntelligenceDamageBonusPerPoint { get; init; } = 0.01f;
}

public class CreatureGeneratorOptions
{
    public int MaxLevel { get; init; } = 100;
    public int MaxSkillLevel { get; init; } = 100;
    public int PointsPerLevel { get; init; } = 5;
}

public class GameClockOptions
{
    public TimeSpan RealTimePerMessage { get; init; } = TimeSpan.FromSeconds(54);
}
