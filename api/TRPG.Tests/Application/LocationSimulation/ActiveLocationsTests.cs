using TRPG.Application.LocationSimulation;

namespace TRPG.Tests.Application.LocationSimulation;

public class ActiveLocationsTests
{
    [Fact]
    public void Distinct_KeepsOnePlayerPerLocation_WhenSeveralPlayersShareIt()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var lowerLevel = new ActiveLocationPlayer(locationId, Guid.NewGuid(), PlayerLevel: 2);
        var higherLevel = new ActiveLocationPlayer(locationId, Guid.NewGuid(), PlayerLevel: 5);

        // Act
        var distinct = ActiveLocations.Distinct([lowerLevel, higherLevel]);

        // Assert
        Assert.Equal(higherLevel, Assert.Single(distinct));
    }

    [Fact]
    public void Distinct_KeepsEveryLocation_WhenPlayersAreSpreadAcrossThem()
    {
        // Arrange
        var first = new ActiveLocationPlayer(Guid.NewGuid(), Guid.NewGuid(), PlayerLevel: 1);
        var second = new ActiveLocationPlayer(Guid.NewGuid(), Guid.NewGuid(), PlayerLevel: 1);

        // Act
        var distinct = ActiveLocations.Distinct([first, second]);

        // Assert
        Assert.Equal(2, distinct.Count);
    }

    [Fact]
    public void Distinct_PicksTheLowestPlayerId_WhenLevelsTie()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var lowerId = new ActiveLocationPlayer(
            locationId,
            new Guid("00000000-0000-0000-0000-000000000001"),
            PlayerLevel: 3
        );
        var higherId = new ActiveLocationPlayer(
            locationId,
            new Guid("00000000-0000-0000-0000-000000000002"),
            PlayerLevel: 3
        );

        // Act
        var distinct = ActiveLocations.Distinct([higherId, lowerId]);

        // Assert
        Assert.Equal(lowerId, Assert.Single(distinct));
    }
}
