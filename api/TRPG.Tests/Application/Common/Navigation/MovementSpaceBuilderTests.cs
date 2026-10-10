using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Common.Navigation;

public class MovementSpaceBuilderTests
{
    private static readonly Location Location = Builders.MakeLocation(width: 30, depth: 20);

    [Fact]
    public void Build_UsesTheLocationExtent()
    {
        // Arrange & Act
        var space = MovementSpaceBuilder.Build(Location, [], [], []);

        // Assert
        Assert.Equal((30d, 20d), (space.Width, space.Depth));
    }

    [Fact]
    public void Build_SkipsPropsThatDoNotBlockMovement()
    {
        // Arrange
        Prop[] props =
        [
            new Furniture
            {
                Model = PropModel.FurnitureTable,
                X = 5,
                Y = 5,
                Width = 2,
                Depth = 1,
            },
            new Sign
            {
                X = 9,
                Y = 9,
                Width = 1,
                Depth = 1,
            },
        ];

        // Act
        var space = MovementSpaceBuilder.Build(Location, props, [], []);

        // Assert
        Assert.Equal([new MovementObstacle(5, 5, 0, 2, 1)], space.Obstacles);
    }

    [Fact]
    public void Build_IncludesBuildings()
    {
        // Arrange
        var building = Builders.MakeBuilding(x: 10, y: 12, width: 6, depth: 4);

        // Act
        var space = MovementSpaceBuilder.Build(Location, [], [building], []);

        // Assert
        Assert.Equal([new MovementObstacle(10, 12, 0, 6, 4)], space.Obstacles);
    }

    [Fact]
    public void Build_PlacesAStairFootprintAheadOfItsExit()
    {
        // Arrange
        var stairs = Builders.MakeLocationConnector(Location.Id);
        stairs.StairDirection = StairDirection.Up;
        stairs.ExitAngle = Math.PI / 2;
        var placed = new PlacedConnector(stairs, new Point(10, 10), new Point(0, 0));

        // Act
        var space = MovementSpaceBuilder.Build(Location, [], [], [placed]);

        // Assert
        var obstacle = Assert.Single(space.Obstacles);
        Assert.Equal(
            (11d, 10d, Math.PI / 2, 1.2, 2d),
            (
                Math.Round(obstacle.X, 6),
                Math.Round(obstacle.Y, 6),
                obstacle.Angle,
                obstacle.Width,
                obstacle.Depth
            )
        );
    }

    [Fact]
    public void Build_SkipsDoorsWithoutStairs()
    {
        // Arrange
        var door = new PlacedConnector(
            Builders.MakeLocationConnector(Location.Id),
            new Point(10, 10),
            new Point(0, 0)
        );

        // Act
        var space = MovementSpaceBuilder.Build(Location, [], [], [door]);

        // Assert
        Assert.Empty(space.Obstacles);
    }
}
