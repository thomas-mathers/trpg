using TRPG.Application.Common.Navigation;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class GeneratedJourneySeederTests
{
    [Fact]
    public void SnapshotJourneyLeg_CopiesTheRouteForPersistence()
    {
        // Arrange
        var sourcePath = new Polyline { Points = [new Point(1, 2), new Point(3, 4)] };
        var source = new DirectedTravelLeg(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            sourcePath
        );

        // Act
        var snapshot = GeneratedJourneySeeder.SnapshotJourneyLeg(Guid.NewGuid(), 0, source);

        // Assert
        Assert.NotSame(sourcePath, snapshot.Path);
        Assert.Equal(sourcePath.Points, snapshot.Path.Points);
        Assert.All(
            snapshot.Path.Points.Zip(sourcePath.Points),
            pair => Assert.NotSame(pair.First, pair.Second)
        );
    }
}
