using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;

namespace TRPG.Application.Encounters;

internal static class SuspicionDetectionChanceCalculator
{
    public static SkillCheckCurve BuildCurve(SuspicionOptions options) =>
        new(
            BaseChance: options.BaseDetectionChance,
            ChanceChangePerSkillLevel: -options.DetectionChanceReductionPerSkillLevel,
            MinimumChance: options.MinimumDetectionChance,
            MaximumChance: options.MaximumDetectionChance
        );
}
