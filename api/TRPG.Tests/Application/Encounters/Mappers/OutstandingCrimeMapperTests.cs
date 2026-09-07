using TRPG.Application.Crimes.Queries;
using TRPG.Application.Encounters.Mappers;

namespace TRPG.Tests.Application.Encounters.Mappers;

public sealed class OutstandingCrimeMapperTests
{
    private static readonly Guid GuardId = Guid.NewGuid();

    [Fact]
    public void ToOffenseText_AddressesTheGuardDirectly_WhenTheyAreTheOneWhoWasWronged()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, GuardId, ["Blazing Kris"]);

        // Act
        var text = crime.ToOffenseText(GuardId);

        // Assert
        Assert.Equal("Stole Blazing Kris from you", text);
    }

    [Fact]
    public void ToOffenseText_NamesTheVictim_WhenSomeoneElseWasWronged()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, Guid.NewGuid(), ["Blazing Kris"]);

        // Act
        var text = crime.ToOffenseText(GuardId);

        // Assert
        Assert.Equal("Stole Blazing Kris from Cora", text);
    }

    [Fact]
    public void ToOffenseText_OmitsTheItemList_WhenNothingWasCarriedOff()
    {
        // Arrange
        var crime = MakeCrime(OutstandingCrimeKind.Theft, GuardId, []);

        // Act
        var text = crime.ToOffenseText(GuardId);

        // Assert
        Assert.Equal("Stole from you", text);
    }

    [Theory]
    [InlineData(OutstandingCrimeKind.Kill, "Killed you")]
    [InlineData(OutstandingCrimeKind.Assault, "Assaulted you")]
    public void ToOffenseText_DescribesViolentCrimesByTheirVictim(
        OutstandingCrimeKind kind,
        string expected
    )
    {
        // Arrange
        var crime = MakeCrime(kind, GuardId, []);

        // Act
        var text = crime.ToOffenseText(GuardId);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void ToOffenseText_DistinguishesAJailbreakFromAnOrdinaryBreakIn()
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
        var text = jailbreak.ToOffenseText(GuardId);

        // Assert
        Assert.Equal("Broke out of The Darkstead Jail", text);
    }

    private static OutstandingCrime MakeCrime(
        OutstandingCrimeKind kind,
        Guid subjectCreatureId,
        IReadOnlyCollection<string> itemNames
    ) => new(DateTime.UtcNow, kind, "Cora", subjectCreatureId, itemNames, IsJailbreak: false);
}
