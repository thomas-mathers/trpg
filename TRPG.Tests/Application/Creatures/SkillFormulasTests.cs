using TRPG.Application.Creatures;

namespace TRPG.Tests.Application.Creatures;

public class SkillFormulasTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(36, 665)]
    [InlineData(100, 5049)]
    public void CalculateExperienceFromSkillLevel_ReturnsTriangularValue_ForSkillLevel(
        int skillLevel,
        int expected
    )
    {
        // Act
        var contribution = SkillFormulas.CalculateExperienceFromSkillLevel(skillLevel);

        // Assert
        Assert.Equal(expected, contribution);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 3)]
    [InlineData(10, 99)]
    [InlineData(100, 9999)]
    public void CalculateExperienceFromLevel_ReturnsSquareMinusOne_ForLevel(int level, int expected)
    {
        // Act
        var xp = SkillFormulas.CalculateExperienceFromLevel(level);

        // Assert
        Assert.Equal(expected, xp);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 150)]
    [InlineData(3, 307)]
    [InlineData(5, 647)]
    public void CalculateSkillExperienceFromSkillLevel_GrowsExponentially_WithLevel(
        int level,
        int expected
    )
    {
        // Act
        var xp = SkillFormulas.CalculateSkillExperienceFromSkillLevel(level);

        // Assert
        Assert.Equal(expected, xp);
    }

    [Fact]
    public void CalculateLevelFromSkillLevels_ReturnsOne_WhenAllSkillsAreAtTheFloor()
    {
        // Act
        var level = SkillFormulas.CalculateLevelFromSkillLevels([1, 1, 1, 1, 1, 1, 1]);

        // Assert
        Assert.Equal(1, level);
    }

    [Fact]
    public void CalculateLevelFromSkillLevels_SumsContributions_AcrossSkills()
    {
        // Act — two skills at level 2 contribute 2 + 2 = 4, meeting the level-2 threshold of 3,
        // which a single level-2 skill (contributing 2) would not
        var level = SkillFormulas.CalculateLevelFromSkillLevels([2, 2, 1, 1, 1, 1, 1]);

        // Assert
        Assert.Equal(2, level);
    }

    [Fact]
    public void GetExperienceProgress_StartsAtZero_ForAFreshLevelOneCreature()
    {
        // Act
        var progress = SkillFormulas.GetExperienceProgress(level: 1, experience: 0);

        // Assert
        Assert.Equal(0, progress.Current);
        Assert.Equal(3, progress.ToNextLevel);
    }
}
