using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class StairPlanTests
{
    [Fact]
    public void Exit_PlacesTheFlightOnTheNorthWallFacingIntoTheRoom()
    {
        // Arrange
        var connectorId = Guid.NewGuid();

        // Act
        var exit = StairPlan.Exit(connectorId, roomWidth: 10, lowerFloorNumber: 0);

        // Assert
        Assert.Equal(0, exit.Point.Y);
        Assert.Equal(Math.PI, exit.FacingAngle);
    }

    [Theory]
    [InlineData(2.5)]
    [InlineData(5.25)]
    [InlineData(20)]
    public void Exit_PutsTheSameFlightAtTheSamePlanOffset_InRoomsOfDifferentWidths(double width)
    {
        // Arrange
        var narrow = StairPlan.Exit(Guid.NewGuid(), roomWidth: 2.5, lowerFloorNumber: 0);

        // Act
        var exit = StairPlan.Exit(Guid.NewGuid(), width, lowerFloorNumber: 0);

        // Assert
        Assert.Equal(narrow.Point.X - 2.5 / 2, exit.Point.X - width / 2, precision: 6);
    }

    [Fact]
    public void Exit_SeparatesTheFlightsOfConsecutiveFloors()
    {
        // Arrange
        var first = StairPlan.Exit(Guid.NewGuid(), roomWidth: 2.5, lowerFloorNumber: 0);

        // Act
        var second = StairPlan.Exit(Guid.NewGuid(), roomWidth: 2.5, lowerFloorNumber: 1);

        // Assert
        Assert.Equal(StairPlan.FlightSpacing, second.Point.X - first.Point.X, precision: 6);
    }
}
