using TRPG.Application.Configuration;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class TheftDetectionCalculatorTests
{
    private static readonly TheftOptions Options = new()
    {
        BaseDetectionChance = 0.5f,
        MaximumDetectionChance = 0.8f,
        MinimumDetectionChance = 0.2f,
        SneakChanceReductionPerLevel = 0.1f,
    };

    [Fact]
    public void CalculateChance_ReturnsBaseChance_WhenSneakLevelIsZero()
    {
        // Act
        var chance = TheftDetectionCalculator.CalculateChance(sneakLevel: 0, Options);

        // Assert
        Assert.Equal(0.5f, chance);
    }

    [Fact]
    public void CalculateChance_ReducesDetectionChance_ForEverySneakLevel()
    {
        // Act
        var chance = TheftDetectionCalculator.CalculateChance(sneakLevel: 2, Options);

        // Assert
        Assert.Equal(0.3f, chance);
    }

    [Fact]
    public void CalculateChance_ClampsAtConfiguredMinimum_WhenSneakLevelIsHigh()
    {
        // Act
        var chance = TheftDetectionCalculator.CalculateChance(sneakLevel: 10, Options);

        // Assert
        Assert.Equal(Options.MinimumDetectionChance, chance);
    }

    [Fact]
    public void CalculateChance_ClampsAtConfiguredMaximum_WhenBaseChanceExceedsIt()
    {
        // Arrange
        var options = new TheftOptions
        {
            BaseDetectionChance = 0.9f,
            MaximumDetectionChance = 0.8f,
            MinimumDetectionChance = 0.2f,
            SneakChanceReductionPerLevel = 0.1f,
        };

        // Act
        var chance = TheftDetectionCalculator.CalculateChance(sneakLevel: 0, options);

        // Assert
        Assert.Equal(options.MaximumDetectionChance, chance);
    }
}
