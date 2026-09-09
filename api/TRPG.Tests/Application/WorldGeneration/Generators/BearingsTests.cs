using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BearingsTests
{
    private static readonly Point Origin = new(50, 50);

    [Theory]
    [InlineData(50, 60, CompassDirection.North)]
    [InlineData(60, 60, CompassDirection.Northeast)]
    [InlineData(60, 50, CompassDirection.East)]
    [InlineData(60, 40, CompassDirection.Southeast)]
    [InlineData(50, 40, CompassDirection.South)]
    [InlineData(40, 40, CompassDirection.Southwest)]
    [InlineData(40, 50, CompassDirection.West)]
    [InlineData(40, 60, CompassDirection.Northwest)]
    public void Between_ReadsTheCompassPoint(double x, double y, CompassDirection expected)
    {
        // Act
        var direction = Bearings.Between(Origin, new Point(x, y));

        // Assert
        Assert.Equal(expected, direction);
    }

    [Theory]
    [InlineData(50, 90)]
    [InlineData(90, 90)]
    [InlineData(20, 55)]
    public void Between_ReversesWhenTheJourneyDoes(double x, double y)
    {
        // Arrange — walking back through a passage has to read as the opposite way, or the player
        // cannot build a map from what they are told.
        var destination = new Point(x, y);

        // Act
        var outward = Bearings.Between(Origin, destination);

        // Assert
        var back = Bearings.Between(destination, Origin);
        Assert.Equal(4, Math.Abs((int)outward - (int)back));
    }

    [Fact]
    public void ToWords_ReadsAsProse_SoTheNarratorCanUseIt()
    {
        // Act
        var words = Bearings.ToWords(CompassDirection.Southwest);

        // Assert
        Assert.Equal("southwest", words);
    }
}
