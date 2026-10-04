using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class ExitKeepOutTests
{
    [Fact]
    public void Of_ReturnsASquareHalfAheadOfTheExit_ForADoor()
    {
        // Arrange
        var point = new PlanarPoint(5, 0);

        // Act
        var box = ExitKeepOut.Of(point, Math.PI, stairs: null);

        // Assert
        Assert.Equal(5, box.CenterX, 6);
        Assert.Equal(0.75, box.CenterY, 6);
        Assert.Equal(1.5, box.Width, 6);
        Assert.Equal(1.5, box.Depth, 6);
    }

    [Theory]
    [InlineData(Math.PI, 5, 1)]
    [InlineData(0, 5, 9)]
    public void Of_ReturnsTheStairFootprintAheadOfTheExit_ForAFlightOfStairs(
        double facingAngle,
        double expectedCenterX,
        double expectedCenterY
    )
    {
        // Arrange
        var point = new PlanarPoint(5, facingAngle == 0 ? 10 : 0);

        // Act
        var box = ExitKeepOut.Of(point, facingAngle, StairDirection.Up);

        // Assert
        Assert.Equal(expectedCenterX, box.CenterX, 6);
        Assert.Equal(expectedCenterY, box.CenterY, 6);
        Assert.Equal(StairPlan.Width, box.Width, 6);
        Assert.Equal(StairPlan.Depth, box.Depth, 6);
    }

    [Fact]
    public void Of_SwapsTheStairExtents_WhenTheFlightFacesEastOrWest()
    {
        // Arrange
        var point = new PlanarPoint(0, 5);

        // Act
        var box = ExitKeepOut.Of(point, Math.PI / 2, StairDirection.Down);

        // Assert
        Assert.Equal(StairPlan.Depth, box.Width, 6);
        Assert.Equal(StairPlan.Width, box.Depth, 6);
    }
}
