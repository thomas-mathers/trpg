using TRPG.Domain.Models;
using TRPG.Encounters.Mappers;

namespace TRPG.Tests.Encounters;

public sealed class GuardEncounterOffenseMapperTests
{
    [Fact]
    public void ToText_NamesTheGuard_WhenSheWasTheOneRobbed()
    {
        // Arrange — "you" is the player in every other sentence the narrator writes, and the
        // player reads this list too, so the victim is named either way.
        var offense = new GuardEncounterOffense(
            "Stole Blazing Kris from",
            "Talia Kestrel",
            SubjectIsTheGuard: true
        );

        // Act
        var text = offense.ToText();

        // Assert
        Assert.Equal("Stole Blazing Kris from Talia Kestrel", text);
    }

    [Fact]
    public void ToText_NamesTheSubject_WhenTheGuardWasNotTheVictim()
    {
        // Arrange
        var offense = new GuardEncounterOffense(
            "Broke into",
            "The Silver Setting",
            SubjectIsTheGuard: false
        );

        // Act
        var text = offense.ToText();

        // Assert
        Assert.Equal("Broke into The Silver Setting", text);
    }
}
