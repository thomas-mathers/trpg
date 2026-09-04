using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;

namespace TRPG.Application.Encounters;

internal static class SneakChanceCalculator
{
    public static SkillCheckCurve BuildHostileDetectionCurve(SneakOptions options) =>
        new(
            BaseChance: options.BaseHostileDetectionChance,
            ChanceChangePerSkillLevel: -options.HostileDetectionChanceReductionPerSkillLevel,
            MinimumChance: options.MinimumHostileDetectionChance,
            MaximumChance: options.MaximumHostileDetectionChance
        );
}
