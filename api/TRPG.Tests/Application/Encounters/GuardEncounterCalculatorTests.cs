using TRPG.Application.Configuration;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class GuardEncounterCalculatorTests
{
    private static readonly GuardEncounterOptions Options = new()
    {
        FineGoldPerReputationPoint = 5f,
        MaxFineGold = 250,
        JailHoursPerReputationPoint = 0.5f,
        MaxJailHours = 24,
    };

    [Fact]
    public void ComputeFineGold_ScalesWithTheMagnitudeOfTheReputationScore()
    {
        // Act
        var fine = GuardEncounterCalculator.ComputeFineGold(-10, Options);

        // Assert
        Assert.Equal(50, fine);
    }

    [Fact]
    public void ComputeFineGold_ClampsAtTheConfiguredMaximum()
    {
        // Act
        var fine = GuardEncounterCalculator.ComputeFineGold(-100, Options);

        // Assert
        Assert.Equal(Options.MaxFineGold, fine);
    }

    [Fact]
    public void ComputeJailHours_ScalesWithTheMagnitudeOfTheReputationScore()
    {
        // Act
        var jailHours = GuardEncounterCalculator.ComputeJailHours(-10, Options);

        // Assert
        Assert.Equal(5, jailHours);
    }

    [Fact]
    public void ComputeJailHours_ClampsAtTheConfiguredMaximum()
    {
        // Act
        var jailHours = GuardEncounterCalculator.ComputeJailHours(-100, Options);

        // Assert
        Assert.Equal(Options.MaxJailHours, jailHours);
    }
}
