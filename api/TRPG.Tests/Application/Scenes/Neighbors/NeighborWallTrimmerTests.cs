using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Neighbors;

public class NeighborWallTrimmerTests
{
    private static readonly BoundarySegment InnerStub = Wall(new Placement(47, 0, 0), 6, 0.4);
    private static readonly BoundarySegment EastRoofline = Wall(
        new Placement(50, -1.5, 0),
        20,
        0.4
    );

    [Fact]
    public void Trim_RemovesAWallLyingEntirelyInsideAnotherNeighbor()
    {
        // Arrange
        var north = Neighbor(new Placement(0, -20, 0), new Footprint(50, 20), InnerStub);
        var east = Neighbor(new Placement(40, -1.5, 0), new Footprint(20, 20), EastRoofline);

        // Act
        var trimmed = NeighborWallTrimmer.Trim([north, east]);

        // Assert
        Assert.Empty(trimmed.First(neighbor => neighbor.LocationId == north.LocationId).Segments);
    }

    [Fact]
    public void Trim_ClipsAWallToTheStretchOutsideAnotherNeighbor()
    {
        // Arrange
        var north = Neighbor(new Placement(0, -20, 0), new Footprint(50, 20), InnerStub);
        var east = Neighbor(new Placement(40, -1.5, 0), new Footprint(20, 20), EastRoofline);

        // Act
        var trimmed = NeighborWallTrimmer.Trim([north, east]);

        // Assert
        var segments = trimmed.First(neighbor => neighbor.LocationId == east.LocationId).Segments;
        Assert.Equal([Wall(new Placement(55, -1.5, 0), 10, 0.4)], segments);
    }

    [Fact]
    public void Trim_ClipsAWallRunningAlongAnotherNeighborsEdge()
    {
        // Arrange
        var north = Neighbor(new Placement(-6, -20, 0), new Footprint(50, 20), InnerStub);
        var west = Neighbor(
            new Placement(-30, 0, 0),
            new Footprint(30, 20),
            Wall(new Placement(-15, 0, 0), 30, 0.4)
        );

        // Act
        var trimmed = NeighborWallTrimmer.Trim([north, west]);

        // Assert
        var segments = trimmed.First(neighbor => neighbor.LocationId == west.LocationId).Segments;
        Assert.Equal([Wall(new Placement(-18, 0, 0), 24, 0.4)], segments);
    }

    [Fact]
    public void Trim_KeepsAWallThatOnlyTouchesAnotherNeighborsCorner()
    {
        // Arrange
        var westWall = Wall(new Placement(-6, -10, 0), 0.4, 20);
        var north = Neighbor(new Placement(-6, -20, 0), new Footprint(50, 20), westWall);
        var west = Neighbor(new Placement(-30, 0, 0), new Footprint(30, 20), InnerStub);

        // Act
        var trimmed = NeighborWallTrimmer.Trim([north, west]);

        // Assert
        var segments = trimmed.First(neighbor => neighbor.LocationId == north.LocationId).Segments;
        Assert.Equal([westWall], segments);
    }

    [Fact]
    public void TrimAround_OpensAWall_WhereItCrossesABuilding()
    {
        // Arrange
        var west = Neighbor(
            new Placement(-40, 0, 0),
            new Footprint(40, 20),
            Wall(new Placement(20, 10, 0), 40, 0.4)
        );
        var building = Building(new Placement(20, 10, 0), new Footprint(10, 10));

        // Act
        var trimmed = NeighborWallTrimmer.TrimAround([west], [building]);

        // Assert
        Assert.Equal(
            [Wall(new Placement(7.5, 10, 0), 15, 0.4), Wall(new Placement(32.5, 10, 0), 15, 0.4)],
            trimmed.Single().Segments
        );
    }

    [Fact]
    public void TrimAround_SwapsTheFootprint_WhenTheBuildingIsTurnedSideways()
    {
        // Arrange
        var west = Neighbor(
            new Placement(-40, 0, 0),
            new Footprint(40, 20),
            Wall(new Placement(20, 10, 0), 40, 0.4)
        );
        var building = Building(new Placement(20, 10, Math.PI / 2), new Footprint(10, 20));

        // Act
        var trimmed = NeighborWallTrimmer.TrimAround([west], [building]);

        // Assert
        Assert.Equal(
            [Wall(new Placement(5, 10, 0), 10, 0.4), Wall(new Placement(35, 10, 0), 10, 0.4)],
            trimmed.Single().Segments
        );
    }

    [Fact]
    public void TrimAround_KeepsAWall_ThatPassesBesideABuilding()
    {
        // Arrange
        var wall = Wall(new Placement(20, 10, 0), 40, 0.4);
        var west = Neighbor(new Placement(-40, 0, 0), new Footprint(40, 20), wall);
        var building = Building(new Placement(20, 20, 0), new Footprint(10, 10));

        // Act
        var trimmed = NeighborWallTrimmer.TrimAround([west], [building]);

        // Assert
        Assert.Equal([wall], trimmed.Single().Segments);
    }

    private static SceneNearbyBuildingInfo Building(Placement placement, Footprint footprint) =>
        new(Guid.NewGuid(), "Building", BuildingType.House, placement, footprint, 1);

    private static NeighborDistrict Neighbor(
        Placement origin,
        Footprint size,
        BoundarySegment segment
    ) => new(Guid.NewGuid(), origin, size, [], [], [segment], []);

    private static BoundarySegment Wall(Placement placement, double width, double depth) =>
        new(BoundarySegmentKind.Wall, placement, new Footprint(width, depth));
}
