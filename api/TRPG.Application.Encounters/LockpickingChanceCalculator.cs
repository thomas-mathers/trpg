using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;

namespace TRPG.Application.Encounters;

internal static class LockpickingChanceCalculator
{
    public static SkillCheckCurve BuildLockOpenCurve(int lockLevel, LockpickingOptions options) =>
        new(
            BaseChance: options.BaseChance + lockLevel * options.ChancePerLockLevel,
            ChanceChangePerSkillLevel: options.ChancePerSkillLevel,
            MinimumChance: options.MinimumChance,
            MaximumChance: options.MaximumChance
        );

    public static SkillCheckCurve BuildDetectionCurve(LockpickingOptions options) =>
        new(
            BaseChance: options.BaseDetectionChance,
            ChanceChangePerSkillLevel: -options.DetectionChanceReductionPerSkillLevel,
            MinimumChance: options.MinimumDetectionChance,
            MaximumChance: options.MaximumDetectionChance
        );
}
