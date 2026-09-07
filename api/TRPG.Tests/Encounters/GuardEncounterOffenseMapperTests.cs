using TRPG.Domain.Models;
using TRPG.Encounters.Mappers;

namespace TRPG.Tests.Encounters;

public sealed class GuardEncounterOffenseMapperTests
{
    [Fact]
    public void ToNarratorText_AddressesTheGuardDirectly_WhenSheWasTheOneRobbed()
    {
        // Arrange
        var offense = new GuardEncounterOffense(
            "Stole Blazing Kris from",
            "Talia Kestrel",
            SubjectIsTheGuard: true
        );

        // Act
        var text = offense.ToNarratorText();

        // Assert
        Assert.Equal("Stole Blazing Kris from you", text);
    }

    [Fact]
    public void ToPlayerText_NamesTheGuard_WhenSheWasTheOneRobbed()
    {
        // Arrange — "from you" would read to the player as though they were the victim
        var offense = new GuardEncounterOffense(
            "Stole Blazing Kris from",
            "Talia Kestrel",
            SubjectIsTheGuard: true
        );

        // Act
        var text = offense.ToPlayerText();

        // Assert
        Assert.Equal("Stole Blazing Kris from Talia Kestrel", text);
    }

    [Fact]
    public void ToNarratorText_NamesTheSubject_WhenTheGuardWasNotTheVictim()
    {
        // Arrange
        var offense = new GuardEncounterOffense(
            "Broke into",
            "The Silver Setting",
            SubjectIsTheGuard: false
        );

        // Act
        var text = offense.ToNarratorText();

        // Assert
        Assert.Equal("Broke into The Silver Setting", text);
    }

    [Fact]
    public void ToPlayerText_ReadsTheSameAsTheNarratorText_WhenTheGuardWasNotTheVictim()
    {
        // Arrange
        var offense = new GuardEncounterOffense("Killed", "Mara", SubjectIsTheGuard: false);

        // Act
        var text = offense.ToPlayerText();

        // Assert
        Assert.Equal(offense.ToNarratorText(), text);
    }
}
