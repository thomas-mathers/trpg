using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DungeonMapGeometryTests
{
    [Fact]
    public void Reverse_CopiesOwnedPoints_WithoutSharingTheirIdentity()
    {
        // Arrange
        var original = new Polyline
        {
            Points = [new(155, -62), new(125, -62), new(125, -62.5), new(95, -62.5)],
        };

        // Act
        var reversed = DungeonMapGeometry.Reverse(original);

        // Assert
        Assert.Equal(original.Points.AsEnumerable().Reverse(), reversed.Points);
        Assert.All(
            reversed.Points,
            point => Assert.DoesNotContain(original.Points, other => ReferenceEquals(point, other))
        );
    }

    [Fact]
    public void PathBetween_UsesOneDoorAxis_WhenRoomCentresDifferByHalfAUnit()
    {
        // Arrange
        var origin = new Rectangle(155, -70, 179, -54);
        var destination = new Rectangle(238, -70, 262, -55);

        // Act
        var path = DungeonMapGeometry.PathBetween(origin, destination);

        // Assert
        Assert.Equal(2, path.Points.Count);
        Assert.Equal(path.Points[0].Y, path.Points[1].Y);
        Assert.Equal(origin.Right, path.Points[0].X);
        Assert.Equal(destination.Left, path.Points[1].X);
    }
}
