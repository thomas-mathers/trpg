using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class OrientedBoxTests
{
    private const double EighthTurn = Math.PI / 4;
    private const double QuarterTurn = Math.PI / 2;

    [Fact]
    public void Overlaps_ReturnsTrue_WhenAxisAlignedBoxesIntersect()
    {
        // Arrange
        var first = new OrientedBox(1, 1, 2, 2, 0);
        var second = new OrientedBox(2, 1, 2, 2, 0);

        // Act
        var overlaps = first.Overlaps(second);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenBoxesOnlyShareAnEdge()
    {
        // Arrange
        var first = new OrientedBox(1, 1, 2, 2, 0);
        var second = new OrientedBox(3, 1, 2, 2, 0);

        // Act
        var overlaps = first.Overlaps(second);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenBoundingBoxesIntersectButRotatedBoxesDoNot()
    {
        // Arrange
        var axisAligned = new OrientedBox(0, 0, 2, 2, 0);
        var diamond = new OrientedBox(1.9, 1.9, 2, 2, EighthTurn);

        // Act
        var overlaps = axisAligned.Overlaps(diamond);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenRotatedBoxCutsIntoCorner()
    {
        // Arrange
        var axisAligned = new OrientedBox(0, 0, 2, 2, 0);
        var diamond = new OrientedBox(1.6, 1.6, 2, 2, EighthTurn);

        // Act
        var overlaps = axisAligned.Overlaps(diamond);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_SwapsWidthAndDepth_WhenTurnedAQuarter()
    {
        // Arrange
        var turned = new OrientedBox(0, 0, 4, 2, QuarterTurn);
        var probe = new OrientedBox(0, 1.9, 1, 0.1, 0);

        // Act
        var overlaps = turned.Overlaps(probe);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Inflated_MakesBoxesWithinTheMarginOverlap()
    {
        // Arrange
        var first = new OrientedBox(0, 0, 2, 2, 0);
        var second = new OrientedBox(2.4, 0, 2, 2, 0);

        // Act
        var overlaps = first.Inflated(0.5).Overlaps(second);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Inflated_GrowsEverySideByTheMargin()
    {
        // Arrange
        var box = new OrientedBox(0, 0, 2, 3, 0);

        // Act
        var inflated = box.Inflated(0.5);

        // Assert
        Assert.Equal(3, inflated.Width);
        Assert.Equal(4, inflated.Depth);
    }

    [Fact]
    public void IsInside_ReturnsTrue_WhenBoxTouchesTheBounds()
    {
        // Arrange
        var box = new OrientedBox(1, 1, 2, 2, 0);

        // Act
        var isInside = box.IsInside(2, 2);

        // Assert
        Assert.True(isInside);
    }

    [Fact]
    public void IsInside_ReturnsFalse_WhenBoxCrossesABound()
    {
        // Arrange
        var box = new OrientedBox(1.1, 1, 2, 2, 0);

        // Act
        var isInside = box.IsInside(2, 2);

        // Assert
        Assert.False(isInside);
    }

    [Fact]
    public void IsInside_ReturnsFalse_WhenRotatedCornersExceedTheBounds()
    {
        // Arrange
        var diamond = new OrientedBox(1.25, 1.25, 2, 2, EighthTurn);

        // Act
        var isInside = diamond.IsInside(2.5, 2.5);

        // Assert
        Assert.False(isInside);
    }

    [Fact]
    public void IsInside_ReturnsTrue_WhenQuarterTurnedBoxFitsTheSwappedBounds()
    {
        // Arrange
        var turned = new OrientedBox(1, 2, 4, 2, QuarterTurn);

        // Act
        var isInside = turned.IsInside(2, 4);

        // Assert
        Assert.True(isInside);
    }

    [Fact]
    public void IsInside_ReturnsFalse_WhenQuarterTurnedBoxDoesNotFitTheOriginalBounds()
    {
        // Arrange
        var turned = new OrientedBox(1, 2, 4, 2, QuarterTurn);

        // Act
        var isInside = turned.IsInside(4, 2);

        // Assert
        Assert.False(isInside);
    }
}
