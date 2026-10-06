using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class StairPlanTests
{
    [Fact]
    public void Exit_PlacesTheFlightOnTheNorthWallFacingIntoTheRoom()
    {
        // Arrange
        var connectorId = Guid.NewGuid();

        // Act
        var exit = StairPlan.Exit(connectorId, roomWidth: 10, StairDirection.Up);

        // Assert
        Assert.Equal(0, exit.Point.Y);
        Assert.Equal(Math.PI, exit.FacingAngle);
    }

    [Theory]
    [InlineData(2.25)]
    [InlineData(5.25)]
    [InlineData(20)]
    public void Exit_CentersTheFlight_InRoomsOfDifferentWidths(double width)
    {
        // Act
        var exit = StairPlan.Exit(Guid.NewGuid(), width, StairDirection.Up);

        // Assert
        Assert.Equal(width / 2, exit.Point.X, precision: 6);
    }
}
