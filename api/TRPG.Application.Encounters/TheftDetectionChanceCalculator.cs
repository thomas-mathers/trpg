using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;

namespace TRPG.Application.Encounters;

internal static class TheftDetectionChanceCalculator
{
    public static SkillCheckCurve BuildCurve(TheftOptions options, int totalQuantity) =>
        new(
            BaseChance: options.BaseDetectionChance
                + totalQuantity * options.DetectionChanceIncreasePerItem,
            ChanceChangePerSkillLevel: -options.DetectionChanceReductionPerSkillLevel,
            MinimumChance: options.MinimumDetectionChance,
            MaximumChance: options.MaximumDetectionChance
        );
}
