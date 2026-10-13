using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class WallRunTests
{
    [Fact]
    public void Expand_LeavesAGapBetweenNeighbours_WhenFillingAWallWithBenches()
    {
        // Arrange
        var run = new WallRun(PropModel.SeatBench, RecipeWall.West, From: 0.15, To: 0.8);

        // Act
        var items = run.Expand(new Footprint(Width: 12, Depth: 20)).ToArray();

        // Assert
        var tops = items.Select(item => item.Bounds.Top).Order().ToArray();
        var benchLength = items[0].Bounds.Depth;
        Assert.All(
            tops.Zip(tops.Skip(1)),
            pair => Assert.InRange(pair.Second - pair.First - benchLength, 0.5, double.MaxValue)
        );
    }
}
