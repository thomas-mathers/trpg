using TRPG.Application.Routing;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Routing;

public class JourneyProgressTests
{
    private static readonly GameInstant CheckpointedAt = new(new DateTime(2000, 1, 1, 12, 0, 0));

    [Fact]
    public void Project_InterpolatesTheDirectedLegPathFromTheCheckpoint()
    {
        // Arrange
        var journey = JourneyAt(legIndex: 0, progressMeters: 2);
        var leg = Leg(10, [new Point(0, 0), new Point(0, 10), new Point(10, 10)]);

        // Act
        var projection = JourneyProgress.Project(
            journey,
            [leg],
            metersPerGameSecond: 1,
            CheckpointedAt + TimeSpan.FromSeconds(3)
        );

        // Assert
        Assert.Equal(
            (0, 5d, new Point(0, 10)),
            (projection.LegIndex, projection.LegProgressMeters, projection.Position)
        );
    }

    [Fact]
    public void Project_CarriesProgressAcrossLegs()
    {
        // Arrange
        var journey = JourneyAt(legIndex: 0, progressMeters: 8);

        // Act
        var projection = JourneyProgress.Project(
            journey,
            [
                Leg(10, [new Point(0, 0), new Point(10, 0)]),
                Leg(10, [new Point(10, 0), new Point(20, 0)]),
            ],
            metersPerGameSecond: 1,
            CheckpointedAt + TimeSpan.FromSeconds(5)
        );

        // Assert
        Assert.Equal(
            (1, 3d, new Point(13, 0)),
            (projection.LegIndex, projection.LegProgressMeters, projection.Position)
        );
    }

    [Fact]
    public void Project_LeavesAPlannedJourneyAtItsCheckpoint()
    {
        // Arrange
        var journey = JourneyAt(legIndex: 0, progressMeters: 2);
        journey.Status = JourneyStatus.Planned;

        // Act
        var projection = JourneyProgress.Project(
            journey,
            [Leg(10, [new Point(0, 0), new Point(10, 0)])],
            metersPerGameSecond: 1,
            CheckpointedAt + TimeSpan.FromHours(1)
        );

        // Assert
        Assert.Equal(
            (0, 2d, new Point(2, 0)),
            (projection.LegIndex, projection.LegProgressMeters, projection.Position)
        );
    }

    private static Journey JourneyAt(int legIndex, double progressMeters) =>
        new()
        {
            Status = JourneyStatus.Traveling,
            CheckpointLegIndex = legIndex,
            CheckpointLegProgressMeters = progressMeters,
            CheckpointedAt = CheckpointedAt,
        };

    private static JourneyLeg Leg(double distance, IReadOnlyList<Point> points) =>
        new()
        {
            Distance = distance,
            Path = new Polyline { Points = points.ToList() },
        };
}
