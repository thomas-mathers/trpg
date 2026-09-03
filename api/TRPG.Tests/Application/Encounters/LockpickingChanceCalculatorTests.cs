using TRPG.Application.Configuration;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class LockpickingChanceCalculatorTests
{
    private static readonly LockpickingOptions Options = new()
    {
        BaseChance = 0.6f,
        ChancePerLockLevel = -0.15f,
        ChancePerSkillLevel = 0.05f,
        MinimumChance = 0.05f,
        MaximumChance = 0.95f,
        BaseDetectionChance = 0.5f,
        DetectionChanceReductionPerSkillLevel = 0.05f,
        MinimumDetectionChance = 0.05f,
        MaximumDetectionChance = 0.95f,
    };

    [Fact]
    public void BuildLockOpenCurve_ReducesBaseChance_ForEachLockLevel()
    {
        var curve = LockpickingChanceCalculator.BuildLockOpenCurve(lockLevel: 2, Options);

        Assert.Equal(0.3f, curve.BaseChance, precision: 5);
    }

    [Fact]
    public void BuildLockOpenCurve_IncreasesChance_ForEachSkillLevel()
    {
        var curve = LockpickingChanceCalculator.BuildLockOpenCurve(lockLevel: 0, Options);

        Assert.Equal(Options.ChancePerSkillLevel, curve.ChanceChangePerSkillLevel);
    }

    [Fact]
    public void BuildDetectionCurve_ReducesChance_ForEachSkillLevel()
    {
        var curve = LockpickingChanceCalculator.BuildDetectionCurve(Options);

        Assert.Equal(-Options.DetectionChanceReductionPerSkillLevel, curve.ChanceChangePerSkillLevel);
    }
}
