using TRPG.Application.Crimes.Queries;
using TRPG.Application.Encounters.Mappers;

namespace TRPG.Tests.Application.Encounters.Mappers;

public sealed class OutstandingCrimeMapperTests
{
    private static readonly Guid GuardId = Guid.NewGuid();

    [Fact]
    public void ToOffense_MarksTheSubjectAsTheGuard_WhenTheyAreTheOneWhoWasWronged()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, GuardId, ["Blazing Kris"]);

        // Act
        var offense = crime.ToOffense(GuardId);

        // Assert
        Assert.Equal("Stole Blazing Kris from", offense.Action);
        Assert.True(offense.SubjectIsTheGuard);
    }

    [Fact]
    public void ToOffense_LeavesTheSubjectUnmarked_WhenSomeoneElseWasWronged()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, Guid.NewGuid(), ["Blazing Kris"]);

        // Act
        var offense = crime.ToOffense(GuardId);

        // Assert
        Assert.Equal("Cora", offense.SubjectName);
        Assert.False(offense.SubjectIsTheGuard);
    }

    [Fact]
    public void ToOffense_OmitsTheItemList_WhenNothingWasCarriedOff()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, GuardId, []);

        // Act
        var offense = crime.ToOffense(GuardId);

        // Assert
        Assert.Equal("Stole from", offense.Action);
    }

    [Theory]
    [InlineData(OutstandingCrimeKind.Kill, "Killed")]
    [InlineData(OutstandingCrimeKind.Assault, "Assaulted")]
    public void ToOffense_DescribesViolentCrimesByTheirVictim(
        OutstandingCrimeKind kind,
        string expected
    )
    {
        // Arrange
        var crime = MakeCrime(kind, GuardId, []);

        // Act
        var offense = crime.ToOffense(GuardId);

        // Assert
        Assert.Equal(expected, offense.Action);
    }

    [Fact]
    public void ToOffense_DistinguishesAJailbreakFromAnOrdinaryBreakIn()
    {
        // Arrange
        var jailbreak = new OutstandingCrime(
            DateTime.UtcNow,
            OutstandingCrimeKind.Lockpicking,
            "The Darkstead Jail",
            null,
            [],
            IsJailbreak: true
        );

        // Act
        var offense = jailbreak.ToOffense(GuardId);

        // Assert
        Assert.Equal("Broke out of", offense.Action);
        Assert.Equal("The Darkstead Jail", offense.SubjectName);
        Assert.False(offense.SubjectIsTheGuard);
    }

    private static OutstandingCrime MakeCrime(
        OutstandingCrimeKind kind,
        Guid subjectCreatureId,
        IReadOnlyCollection<string> itemNames
    ) => new(DateTime.UtcNow, kind, "Cora", subjectCreatureId, itemNames, IsJailbreak: false);
}
