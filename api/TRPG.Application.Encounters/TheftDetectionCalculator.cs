using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal static class TheftDetectionCalculator
{
    public static float CalculateChance(int sneakLevel, TheftOptions options) =>
        Math.Clamp(
            options.BaseDetectionChance - sneakLevel * options.SneakChanceReductionPerLevel,
            options.MinimumDetectionChance,
            options.MaximumDetectionChance
        );
}
