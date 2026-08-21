namespace TRPG.Application.CreatureFormulas;

public record SkillCheckCurve(
    float BaseChance,
    float ChanceChangePerSkillLevel,
    float MinimumChance,
    float MaximumChance
);

public static class SkillCheckCalculator
{
    public static float CalculateChance(int skillLevel, SkillCheckCurve curve) =>
        Math.Clamp(
            curve.BaseChance + skillLevel * curve.ChanceChangePerSkillLevel,
            curve.MinimumChance,
            curve.MaximumChance
        );
}
