using TRPG.Application.Routing;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing;

public class JourneyPaceTests
{
    [Fact]
    public void MetersPerGameSecond_UsesTheSlowestJourneyMember()
    {
        // Arrange
        var faster = Builders.MakeCreature(movementSpeed: 80);
        var slower = Builders.MakeCreature(movementSpeed: 40);

        // Act
        var pace = JourneyPace.MetersPerGameSecond([faster, slower], timeScale: 2);

        // Assert
        Assert.Equal(0.672, pace, 3);
    }
}
