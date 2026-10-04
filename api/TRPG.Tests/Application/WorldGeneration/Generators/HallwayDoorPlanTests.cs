using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class HallwayDoorPlanTests
{
    private static readonly Footprint Hallway = new(Width: 2.5, Depth: 24);

    [Fact]
    public void Place_AlternatesDoorsBetweenTheEastAndWestWalls()
    {
        // Arrange
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var exits = HallwayDoorPlan.Place(Hallway, ids, [3.5, 3.5, 3.5, 3.5]);

        // Assert
        Assert.Equal([2.5, 0, 2.5, 0], exits.Select(exit => exit.Point.X));
    }

    [Fact]
    public void Place_CentersEachDoorOnItsRoomWithEvenGapsAlongTheWall()
    {
        // Arrange
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var exits = HallwayDoorPlan.Place(Hallway, ids, [3.5, 3.5, 3.5, 3.5]);

        // Assert
        var gap = (24 - 7) / 3.0;
        Assert.Equal(gap + 1.75, exits[0].Point.Y, precision: 6);
        Assert.Equal(2 * gap + 3.5 + 1.75, exits[2].Point.Y, precision: 6);
        Assert.Equal(exits[0].Point.Y, exits[1].Point.Y, precision: 6);
    }

    [Fact]
    public void Place_FacesEachDoorInwardFromItsWall()
    {
        // Arrange
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var exits = HallwayDoorPlan.Place(Hallway, ids, [3.5, 3.5]);

        // Assert
        Assert.Equal([3 * Math.PI / 2, Math.PI / 2], exits.Select(exit => exit.FacingAngle));
    }

    [Fact]
    public void Place_KeepsTheConnectorOrder()
    {
        // Arrange
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var exits = HallwayDoorPlan.Place(Hallway, ids, [3.5, 3.5, 3.5]);

        // Assert
        Assert.Equal(ids, exits.Select(exit => exit.ConnectorId));
    }
}
