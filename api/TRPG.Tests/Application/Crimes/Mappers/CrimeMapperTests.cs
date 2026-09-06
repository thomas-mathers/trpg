using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Crimes.Mappers;

public sealed class CrimeMapperTests
{
    private static readonly ReputationOptions Options = new();
    private static readonly Guid FactionId = Guid.NewGuid();
    private static readonly Guid WitnessId = Guid.NewGuid();

    [Fact]
    public void ToCrimeReport_CarriesEveryFactionTheVictimBelongedTo_ForAKill()
    {
        // Arrange
        var otherFactionId = Guid.NewGuid();
        var crime = new KillCrime { VictimFactionIds = [FactionId, otherFactionId] };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal([FactionId, otherFactionId], report.FactionIds);
        Assert.Equal([WitnessId], report.ReportedWitnessIds);
        Assert.Equal(Options.KillReputationPenalty, report.Penalty);
    }

    [Fact]
    public void ToCrimeReport_CarriesNoFactions_WhenTheVictimHadNone()
    {
        // Arrange
        var crime = new AssaultCrime { VictimFactionIds = [] };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Empty(report.FactionIds);
        Assert.Equal(Options.AssaultReputationPenalty, report.Penalty);
    }

    [Theory]
    [InlineData(null, false, -25)]
    [InlineData(TheftCrimeOutcome.Taken, false, -25)]
    [InlineData(TheftCrimeOutcome.Apologized, false, -10)]
    public void ToCrimeReport_PricesATheftByItsOutcome(
        TheftCrimeOutcome? outcome,
        bool _,
        int expectedPenalty
    )
    {
        // Arrange
        var crime = new TheftCrime { OwnerFactionId = FactionId, Outcome = outcome };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal([FactionId], report.FactionIds);
        Assert.Equal(expectedPenalty, report.Penalty);
    }

    [Theory]
    [InlineData(false, null, -10)]
    [InlineData(false, LockpickingCrimeOutcome.SettledWithGuard, -4)]
    [InlineData(true, null, -50)]
    [InlineData(true, LockpickingCrimeOutcome.SettledWithGuard, -20)]
    public void ToCrimeReport_RanksAJailbreakAboveOrdinaryLockpicking(
        bool isJailbreak,
        LockpickingCrimeOutcome? outcome,
        int expectedPenalty
    )
    {
        // Arrange
        var crime = new LockpickingCrime
        {
            OwnerFactionId = FactionId,
            IsJailbreak = isJailbreak,
            Outcome = outcome,
        };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal(expectedPenalty, report.Penalty);
    }

    [Fact]
    public void ToCrimeReport_PricesATrespassAtTheFlatPenalty()
    {
        // Arrange
        var crime = new TrespassingCrime { OwnerFactionId = FactionId };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal([FactionId], report.FactionIds);
        Assert.Equal(Options.TrespassingReputationPenalty, report.Penalty);
    }
}
