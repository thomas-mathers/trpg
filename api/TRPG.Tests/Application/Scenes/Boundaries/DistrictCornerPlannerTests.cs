using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Boundaries;

public class DistrictCornerPlannerTests
{
    private static readonly Footprint Size = new(40, 30);

    [Fact]
    public void Seal_ExtendsTheOverhangingWall_DownToTheInsetNeighbor()
    {
        // Arrange
        var north = NorthNeighbor();
        var west = WestNeighbor(top: 6);

        // Act
        var sealedNeighbors = DistrictCornerPlanner.Seal(Size, [north, west]);

        // Assert
        var extension = Assert.Single(
            sealedNeighbors.First(neighbor => neighbor.LocationId == north.LocationId).Segments,
            segment => segment.Placement is { X: -10, Y: > 0 }
        );
        Assert.Equal(new Placement(-10, 3, 0), extension.Placement);
        Assert.Equal(new Footprint(DistrictBoundaryPlanner.WallThickness, 6), extension.Footprint);
    }

    [Fact]
    public void Seal_RemovesTheOverhangingStub_InsideThePocket()
    {
        // Arrange
        var north = NorthNeighbor();
        var west = WestNeighbor(top: 6);

        // Act
        var sealedNeighbors = DistrictCornerPlanner.Seal(Size, [north, west]);

        // Assert
        var segments = sealedNeighbors
            .First(neighbor => neighbor.LocationId == north.LocationId)
            .Segments;
        Assert.DoesNotContain(segments, segment => segment.Placement.Y == 0);
    }

    [Fact]
    public void Seal_LeavesNeighborsUntouched_WhenNoneIsInset()
    {
        // Arrange
        var north = NorthNeighbor();
        var west = WestNeighbor(top: 0);

        // Act
        var sealedNeighbors = DistrictCornerPlanner.Seal(Size, [north, west]);

        // Assert
        Assert.Equal(
            north.Segments,
            sealedNeighbors.First(neighbor => neighbor.LocationId == north.LocationId).Segments
        );
    }

    [Fact]
    public void Seal_ExtendsTheOverhangingSideWall_WhenTheNorthNeighborIsInset()
    {
        // Arrange
        var west = new NeighborDistrict(
            Guid.NewGuid(),
            new Placement(-30, -10, 0),
            new Footprint(30, 50),
            [],
            [],
            [Wall(new Placement(0, -5.1, 0), new Footprint(0.4, 10.2))],
            []
        );
        var north = new NeighborDistrict(
            Guid.NewGuid(),
            new Placement(6, -20, 0),
            new Footprint(28, 20),
            [],
            [],
            [],
            []
        );

        // Act
        var sealedNeighbors = DistrictCornerPlanner.Seal(Size, [west, north]);

        // Assert
        var segments = sealedNeighbors
            .First(neighbor => neighbor.LocationId == west.LocationId)
            .Segments;
        var closure = Assert.Single(segments);
        Assert.Equal(new Placement(3, -10, 0), closure.Placement);
        Assert.Equal(new Footprint(6, DistrictBoundaryPlanner.WallThickness), closure.Footprint);
    }

    [Fact]
    public void Resolve_OpensTheOwnWall_AcrossThePocket()
    {
        // Arrange
        var north = NorthNeighbor();
        var west = WestNeighbor(top: 6);
        var toWest = ExitTo(west.LocationId, 0, 18);
        var toNorth = ExitTo(north.LocationId, 20, 0);

        // Act
        var boundary = SceneBoundaryResolver.Resolve(Size, [toWest, toNorth], [north, west]);

        // Assert
        Assert.DoesNotContain(boundary.Segments, segment => segment.Placement.X == 0);
    }

    private static NeighborDistrict NorthNeighbor() =>
        new(
            Guid.NewGuid(),
            new Placement(-10, -20, 0),
            new Footprint(60, 20),
            [],
            [],
            [
                Wall(new Placement(-10, -10, 0), new Footprint(0.4, 20.4)),
                Wall(new Placement(-5.1, 0, 0), new Footprint(10.2, 0.4)),
            ],
            []
        );

    private static NeighborDistrict WestNeighbor(double top) =>
        new(
            Guid.NewGuid(),
            new Placement(-30, top, 0),
            new Footprint(30, 30 - top),
            [],
            [],
            [],
            []
        );

    private static BoundarySegment Wall(Placement placement, Footprint footprint) =>
        new(BoundarySegmentKind.Wall, placement, footprint);

    private static SceneExitInfo ExitTo(Guid destinationLocationId, double x, double y) =>
        new(
            Guid.NewGuid(),
            "A path.",
            new SceneDistrictExitDestination("District", DistrictType.Residential),
            IsLocked: false,
            Direction: null,
            IsVisited: false,
            IsWayBack: false,
            DestinationLocationId: destinationLocationId,
            Placement: new Placement(x, y, 0)
        );
}
