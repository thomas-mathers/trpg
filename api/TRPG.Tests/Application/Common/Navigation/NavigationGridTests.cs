using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Common.Navigation;

public class NavigationGridTests
{
    private const double Cell = 0.5;

    [Fact]
    public void FindPath_ReturnsAStraightLine_WhenNothingIsBlocked()
    {
        // Arrange
        var grid = new NavigationGrid(10, 10, Cell, _ => false);

        // Act
        var path = grid.FindPath(new Point(1.2, 1.2), new Point(8.7, 6.3));

        // Assert
        Assert.Equal([new Point(1.2, 1.2), new Point(8.7, 6.3)], path);
    }

    [Fact]
    public void FindPath_BendsAroundTheObstacle_WhenAWallBlocksTheStraightLine()
    {
        // Arrange
        var grid = new NavigationGrid(10, 10, Cell, point => point.X is > 4 and < 5 && point.Y < 8);

        // Act
        var path = grid.FindPath(new Point(1.25, 2.25), new Point(8.25, 2.25));

        // Assert
        Assert.True(path.Count > 2);
        Assert.All(path, point => Assert.False(point.X is > 4 and < 5 && point.Y < 8));
    }

    [Fact]
    public void FindPath_NeverCrossesBlockedCells()
    {
        // Arrange
        Func<Point, bool> isBlocked = point => point.X is > 4 and < 5 && point.Y < 8;
        var grid = new NavigationGrid(10, 10, Cell, isBlocked);

        // Act
        var path = grid.FindPath(new Point(1.25, 2.25), new Point(8.25, 2.25));

        // Assert
        Assert.All(Segments(path), point => Assert.False(isBlocked(point)));
    }

    [Fact]
    public void FindPath_StartsFromTheNearestFreeCell_WhenTheStartIsBlocked()
    {
        // Arrange
        var grid = new NavigationGrid(
            10,
            10,
            Cell,
            point => point is { X: > 1 and < 2, Y: > 1 and < 2 }
        );

        // Act
        var path = grid.FindPath(new Point(1.5, 1.5), new Point(8.25, 8.25));

        // Assert
        Assert.Equal(new Point(8.25, 8.25), path[^1]);
    }

    [Fact]
    public void FindPath_FallsBackToAStraightLine_WhenTheGoalIsWalledOff()
    {
        // Arrange
        var grid = new NavigationGrid(
            10,
            10,
            Cell,
            point => point.X is > 5 and < 5.5 || point.Y is > 5 and < 5.5
        );

        // Act
        var path = grid.FindPath(new Point(1.25, 1.25), new Point(8.25, 8.25));

        // Assert
        Assert.Equal([new Point(1.25, 1.25), new Point(8.25, 8.25)], path);
    }

    private static IEnumerable<Point> Segments(IReadOnlyList<Point> path)
    {
        for (var index = 1; index < path.Count; index++)
        {
            for (var step = 0; step <= 20; step++)
            {
                var fraction = step / 20d;
                yield return new Point(
                    path[index - 1].X + (path[index].X - path[index - 1].X) * fraction,
                    path[index - 1].Y + (path[index].Y - path[index - 1].Y) * fraction
                );
            }
        }
    }
}
