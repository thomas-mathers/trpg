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
        Assert.Null(report.VictimId);
        Assert.Equal(Options.KillReputationPenalty, report.Penalty);
    }

    [Fact]
    public void ToCrimeReport_CarriesNoFactions_WhenTheVictimHadNone()
    {
        // Arrange
        var victimId = Guid.NewGuid();
        var crime = new AssaultCrime { VictimFactionIds = [], VictimId = victimId };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Empty(report.FactionIds);
        Assert.Equal(victimId, report.VictimId);
        Assert.Equal(Options.AssaultReputationPenalty, report.Penalty);
    }

    [Theory]
    [InlineData(null, false, -9)]
    [InlineData(TheftCrimeOutcome.Taken, false, -9)]
    [InlineData(TheftCrimeOutcome.Apologized, false, -3)]
    public void ToCrimeReport_PricesATheftByItsOutcome(
        TheftCrimeOutcome? outcome,
        bool _,
        int expectedPenalty
    )
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var crime = new TheftCrime
        {
            OwnerFactionId = FactionId,
            OwnerCreatureId = ownerId,
            Outcome = outcome,
        };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal([FactionId], report.FactionIds);
        Assert.Equal(ownerId, report.VictimId);
        Assert.Equal(expectedPenalty, report.Penalty);
    }

    [Theory]
    [InlineData(null, -5)]
    [InlineData(LockpickingCrimeOutcome.SettledWithGuard, -2)]
    public void ToCrimeReport_PricesABreakInByWhetherItWasSettled(
        LockpickingCrimeOutcome? outcome,
        int expectedPenalty
    )
    {
        // Arrange
        var crime = new LockpickingCrime { OwnerFactionIds = [FactionId], Outcome = outcome };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal(expectedPenalty, report.Penalty);
    }

    [Theory]
    [InlineData(null, -20)]
    [InlineData(LockpickingCrimeOutcome.SettledWithGuard, -8)]
    public void ToCrimeReport_RanksAJailbreakFarAboveOrdinaryLockpicking(
        LockpickingCrimeOutcome? outcome,
        int expectedPenalty
    )
    {
        // Arrange
        var crime = new JailbreakCrime { OwnerFactionIds = [FactionId], Outcome = outcome };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal(expectedPenalty, report.Penalty);
        Assert.Null(report.VictimId);
    }

    [Fact]
    public void ToCrimeReport_PricesATrespassAtTheFlatPenalty()
    {
        // Arrange
        var crime = new TrespassingCrime { OwnerFactionIds = [FactionId] };

        // Act
        var report = crime.ToCrimeReport([WitnessId], Options);

        // Assert
        Assert.Equal([FactionId], report.FactionIds);
        Assert.Null(report.VictimId);
        Assert.Equal(Options.TrespassingReputationPenalty, report.Penalty);
    }
}
