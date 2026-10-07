using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomGridTests
{
    [Fact]
    public void CellBox_SnapsTheExtentOutwardToWholeCells()
    {
        // Arrange
        var pose = new Placement(X: 0.85, Y: 0.35, Angle: 0);
        var footprint = new Footprint(Width: 0.7, Depth: 0.5);

        // Act
        var box = RoomGrid.CellBox(pose, footprint);

        // Assert
        Assert.Equal(new OrientedBox(1.0, 0.5, 1.0, 1.0, 0), box);
    }

    [Fact]
    public void CellBox_UsesTheRotatedExtent()
    {
        // Arrange
        var pose = new Placement(X: 1, Y: 1, Angle: Math.PI / 2);
        var footprint = new Footprint(Width: 1.0, Depth: 0.5);

        // Act
        var box = RoomGrid.CellBox(pose, footprint);

        // Assert
        Assert.Equal(1.0, box.Width, precision: 6);
        Assert.Equal(1.0, box.Depth, precision: 6);
    }

    [Fact]
    public void Alignments_PutTheExtentCornerOnTheGridWithoutGrowingTheCellBox()
    {
        // Arrange
        var pose = new Placement(X: 0.85, Y: 0.35, Angle: 0);
        var footprint = new Footprint(Width: 0.7, Depth: 0.5);

        // Act
        var alignments = RoomGrid.Alignments(pose, footprint).ToArray();

        // Assert
        Assert.Equal(2, alignments.Length);
        Assert.All(
            alignments,
            alignment =>
            {
                var box = RoomGrid.CellBox(alignment, footprint);
                Assert.Equal(1.0, box.Width, precision: 6);
                Assert.Equal(0.5, box.Depth, precision: 6);
            }
        );
    }

    [Fact]
    public void Alignments_ListTheNearestShiftFirst()
    {
        // Arrange
        var pose = new Placement(X: 1.1, Y: 1.5, Angle: 0);
        var footprint = new Footprint(Width: 1.0, Depth: 1.0);

        // Act
        var first = RoomGrid.Alignments(pose, footprint).First();

        // Assert
        Assert.Equal(1.0, first.X, precision: 6);
        Assert.Equal(1.5, first.Y, precision: 6);
    }
}
