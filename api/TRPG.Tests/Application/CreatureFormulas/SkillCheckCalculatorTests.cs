using TRPG.Application.CreatureFormulas;

namespace TRPG.Tests.Application.CreatureFormulas;

public class SkillCheckCalculatorTests
{
    private static readonly SkillCheckCurve Curve = new(
        BaseChance: 0.5f,
        ChanceChangePerSkillLevel: -0.1f,
        MinimumChance: 0.2f,
        MaximumChance: 0.8f
    );

    [Fact]
    public void CalculateChance_ReturnsBaseChance_WhenSkillLevelIsZero()
    {
        var chance = SkillCheckCalculator.CalculateChance(skillLevel: 0, Curve);

        Assert.Equal(0.5f, chance);
    }

    [Fact]
    public void CalculateChance_AppliesTheChanceChange_ForEverySkillLevel()
    {
        var chance = SkillCheckCalculator.CalculateChance(skillLevel: 2, Curve);

        Assert.Equal(0.3f, chance);
    }

    [Fact]
    public void CalculateChance_IncreasesChance_WhenTheCurveHasAPositiveChange()
    {
        var curve = Curve with { ChanceChangePerSkillLevel = 0.1f };

        var chance = SkillCheckCalculator.CalculateChance(skillLevel: 2, curve);

        Assert.Equal(0.7f, chance);
    }

    [Fact]
    public void CalculateChance_ClampsAtConfiguredMinimum_WhenSkillLevelIsHigh()
    {
        var chance = SkillCheckCalculator.CalculateChance(skillLevel: 10, Curve);

        Assert.Equal(Curve.MinimumChance, chance);
    }

    [Fact]
    public void CalculateChance_ClampsAtConfiguredMaximum_WhenBaseChanceExceedsIt()
    {
        var curve = Curve with { BaseChance = 0.9f };

        var chance = SkillCheckCalculator.CalculateChance(skillLevel: 0, curve);

        Assert.Equal(curve.MaximumChance, chance);
    }
}
