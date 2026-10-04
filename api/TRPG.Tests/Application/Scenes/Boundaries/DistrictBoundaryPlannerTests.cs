using TRPG.Application.Scenes.Boundaries;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Boundaries;

public class DistrictBoundaryPlannerTests
{
    private static readonly Footprint Size = new(40, 30);

    [Fact]
    public void Plan_WallsEveryEdge_WhenNothingLeavesTheDistrict()
    {
        // Arrange
        BoundaryExit[] exits = [];

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.Empty(boundary.OpenEdges);
        Assert.Empty(boundary.Gates);
        Assert.Equal(4, boundary.Segments.Count);
        Assert.All(
            boundary.Segments,
            segment => Assert.Equal(BoundarySegmentKind.Wall, segment.Kind)
        );
    }

    [Fact]
    public void Plan_LeavesTheEdgeOpen_WhenADistrictConnectorLeavesThroughIt()
    {
        // Arrange
        BoundaryExit[] exits = [Exit(BoundaryExitKind.District, x: 20, y: 30, angle: Math.PI)];

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.Equal([CompassDirection.South], boundary.OpenEdges);
        Assert.Equal(3, boundary.Segments.Count);
        Assert.DoesNotContain(boundary.Segments, segment => segment.Placement.Y == 30);
    }

    [Fact]
    public void Plan_CutsAGateWithTwoTowers_WhenAWildernessConnectorLeavesThroughAWalledEdge()
    {
        // Arrange
        var gate = Exit(BoundaryExitKind.Wilderness, x: 20, y: 30, angle: Math.PI);

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, [gate]);

        // Assert
        Assert.Empty(boundary.OpenEdges);
        Assert.Equal(gate.ConnectorId, Assert.Single(boundary.Gates).ConnectorId);
        Assert.Equal(
            2,
            boundary.Segments.Count(segment => segment.Kind == BoundarySegmentKind.Tower)
        );
        Assert.Equal(
            5,
            boundary.Segments.Count(segment => segment.Kind == BoundarySegmentKind.Wall)
        );
    }

    [Fact]
    public void Plan_NeverCoversAnExitWithAWall()
    {
        // Arrange
        BoundaryExit[] exits =
        [
            Exit(BoundaryExitKind.District, x: 20, y: 0, angle: 0),
            Exit(BoundaryExitKind.District, x: 0, y: 12, angle: Math.PI / 2),
            Exit(BoundaryExitKind.Wilderness, x: 28, y: 30, angle: Math.PI),
        ];

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.All(
            exits,
            exit =>
                Assert.DoesNotContain(
                    boundary.Segments,
                    segment => Covers(segment, exit.Placement.X, exit.Placement.Y)
                )
        );
    }

    [Fact]
    public void Plan_LeavesTheEdgeOpenWithoutAGate_WhenADistrictAndWildernessConnectorShareIt()
    {
        // Arrange
        BoundaryExit[] exits =
        [
            Exit(BoundaryExitKind.District, x: 10, y: 30, angle: Math.PI),
            Exit(BoundaryExitKind.Wilderness, x: 30, y: 30, angle: Math.PI),
        ];

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.Equal([CompassDirection.South], boundary.OpenEdges);
        Assert.Empty(boundary.Gates);
    }

    [Fact]
    public void Plan_IgnoresAnExitInsideTheDistrict()
    {
        // Arrange
        BoundaryExit[] exits = [Exit(BoundaryExitKind.District, x: 20, y: 15, angle: 0)];

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.Empty(boundary.OpenEdges);
        Assert.Equal(4, boundary.Segments.Count);
    }

    [Fact]
    public void Plan_ProducesTheSameBoundary_WhenRunTwice()
    {
        // Arrange
        BoundaryExit[] exits = [Exit(BoundaryExitKind.Wilderness, x: 12, y: 30, angle: Math.PI)];
        var first = DistrictBoundaryPlanner.Plan(Size, exits);

        // Act
        var second = DistrictBoundaryPlanner.Plan(Size, exits);

        // Assert
        Assert.Equal(first.Segments, second.Segments);
    }

    [Fact]
    public void Plan_CentresTheGate_WhenTheEdgeIsTooShortToHoldOne()
    {
        // Arrange
        var gate = Exit(BoundaryExitKind.Wilderness, x: 1, y: 6, angle: Math.PI);

        // Act
        var boundary = DistrictBoundaryPlanner.Plan(new Footprint(6, 6), [gate]);

        // Assert
        Assert.Equal(3, Assert.Single(boundary.Gates).Placement.X);
    }

    private static BoundaryExit Exit(BoundaryExitKind kind, double x, double y, double angle) =>
        new(Guid.NewGuid(), kind, new Placement(x, y, angle));

    private static bool Covers(BoundarySegment segment, double x, double y)
    {
        var (placement, footprint) = (segment.Placement, segment.Footprint);

        return Math.Abs(x - placement.X) < footprint.Width / 2
            && Math.Abs(y - placement.Y) < footprint.Depth / 2;
    }
}
