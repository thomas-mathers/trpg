using TRPG.Application.Configuration;
using TRPG.Application.Encounters;

namespace TRPG.Tests.Application.Encounters;

public class GuardEncounterCalculatorTests
{
    private static readonly GuardEncounterOptions Options = new()
    {
        FineGoldPerReputationPoint = 5f,
        MinimumFineGold = 5,
        MaxFineGold = 250,
        JailHoursPerReputationPoint = 0.5f,
        MinimumJailHours = 1,
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
    public void ComputeFineGold_ClampsAtTheConfiguredMinimum_WhenReputationScoreIsZero()
    {
        // Arrange — a brand-new, neutral-reputation player caught red-handed should never be
        // fined 0 gold, which RemoveGoldCommand rejects outright.
        // Act
        var fine = GuardEncounterCalculator.ComputeFineGold(0, Options);

        // Assert
        Assert.Equal(Options.MinimumFineGold, fine);
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

    [Fact]
    public void ComputeJailHours_ClampsAtTheConfiguredMinimum_WhenReputationScoreIsZero()
    {
        // Arrange — a brand-new, neutral-reputation player caught red-handed should never be
        // offered "serve 0 hours" as a jail sentence.
        // Act
        var jailHours = GuardEncounterCalculator.ComputeJailHours(0, Options);

        // Assert
        Assert.Equal(Options.MinimumJailHours, jailHours);
    }
}
