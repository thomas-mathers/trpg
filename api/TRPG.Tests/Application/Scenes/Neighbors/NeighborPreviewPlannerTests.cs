using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Neighbors;

public class NeighborPreviewPlannerTests
{
    private static readonly Footprint Size = new(40, 30);

    [Fact]
    public void Plan_PlacesTheNeighborBeyondTheNorthEdge_WhenTheExitsMeet()
    {
        // Arrange
        var source = Source(new Footprint(20, 20), reverseExit: new Placement(5, 20, 0));

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        var building = Assert.Single(neighbor!.Buildings);
        Assert.Equal(8, building.Placement.X);
        Assert.Equal(-16, building.Placement.Y);
    }

    [Fact]
    public void Plan_PlacesTheNeighborBeyondTheSouthEdge_WhenTheExitsMeet()
    {
        // Arrange
        var source = Source(new Footprint(20, 20), reverseExit: new Placement(5, 0, 0));

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 30, 0), source);

        // Assert
        var building = Assert.Single(neighbor!.Buildings);
        Assert.Equal(8, building.Placement.X);
        Assert.Equal(34, building.Placement.Y);
    }

    [Fact]
    public void Plan_PlacesTheNeighborBeyondTheEastEdge_WhenTheExitsMeet()
    {
        // Arrange
        var source = Source(
            new Footprint(20, 10),
            reverseExit: new Placement(0, 3, 0),
            building: Building(new Placement(2, 2, 0))
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(40, 12, 0), source);

        // Assert
        var building = Assert.Single(neighbor!.Buildings);
        Assert.Equal(42, building.Placement.X);
        Assert.Equal(11, building.Placement.Y);
    }

    [Fact]
    public void Plan_PlacesTheNeighborBeyondTheWestEdge_WhenTheExitsMeet()
    {
        // Arrange
        var source = Source(
            new Footprint(20, 10),
            reverseExit: new Placement(20, 3, 0),
            building: Building(new Placement(2, 2, 0))
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(0, 12, 0), source);

        // Assert
        var building = Assert.Single(neighbor!.Buildings);
        Assert.Equal(-18, building.Placement.X);
        Assert.Equal(11, building.Placement.Y);
    }

    [Fact]
    public void Plan_KeepsBuildingFacingAndFootprint()
    {
        // Arrange
        var source = Source(
            new Footprint(20, 20),
            reverseExit: new Placement(5, 20, 0),
            building: Building(new Placement(3, 4, Math.PI / 2))
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        var building = Assert.Single(neighbor!.Buildings);
        Assert.Equal(Math.PI / 2, building.Placement.Angle);
        Assert.Equal(new Footprint(6, 5), building.Footprint);
    }

    [Fact]
    public void Plan_WallsTheNeighborsOtherEdgesButNotTheSharedOne()
    {
        // Arrange
        var reverseExit = new Placement(5, 20, 0);
        var source = Source(new Footprint(20, 20), reverseExit);

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        Assert.Equal(3, neighbor!.Segments.Count);
        Assert.DoesNotContain(neighbor.Segments, segment => segment.Placement.Y == 0);
        Assert.Contains(neighbor.Segments, segment => segment.Placement.Y == -20);
    }

    [Fact]
    public void Plan_WallsOnlyTheOverhang_WhenTheNeighborIsWiderThanTheSharedEdge()
    {
        // Arrange
        var cottageRow = Source(
            new Footprint(116, 115.5),
            reverseExit: new Placement(58, 115.5, 0)
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(
            new Footprint(95, 75),
            new Placement(47.5, 0, 0),
            cottageRow
        );

        // Assert
        var seamWalls = neighbor!
            .Segments.Where(segment => segment.Placement.Y == 0)
            .OrderBy(segment => segment.Placement.X)
            .ToArray();
        Assert.Equal(2, seamWalls.Length);
        Assert.Equal(0, seamWalls[0].Placement.X + seamWalls[0].Footprint.Width / 2, 6);
        Assert.Equal(95, seamWalls[1].Placement.X - seamWalls[1].Footprint.Width / 2, 6);
    }

    [Fact]
    public void SharedSpan_ReturnsTheStretchBothDistrictsCover_WhenTheNeighborIsShorterThanTheEdge()
    {
        // Act
        var span = NeighborPreviewPlanner.SharedSpan(
            new Footprint(95, 75),
            CompassDirection.West,
            new Placement(-82, 4.5, 0),
            new Footprint(82, 66)
        );

        // Assert
        Assert.Equal(new EdgeSpan(4.5, 70.5), span);
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenTwoNeighborsShareFloorArea()
    {
        // Arrange
        var first = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(10, 0, 0),
            Source(new Footprint(20, 20), new Placement(5, 20, 0))
        );
        var second = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(20, 0, 0),
            Source(new Footprint(20, 20), new Placement(5, 20, 0))
        );

        // Act
        var overlaps = NeighborPreviewPlanner.Overlaps(first!, second!);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenTwoNeighborsSitSideBySide()
    {
        // Arrange
        var first = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(5, 0, 0),
            Source(new Footprint(20, 20), new Placement(5, 20, 0))
        );
        var second = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(35, 0, 0),
            Source(new Footprint(20, 20), new Placement(5, 20, 0))
        );

        // Act
        var overlaps = NeighborPreviewPlanner.Overlaps(first!, second!);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenTwoNeighborsOnlyShareACornerSliver()
    {
        // Arrange
        var north = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(10, 0, 0),
            Source(new Footprint(50, 20), new Placement(5, 20, 0))
        );
        var east = NeighborPreviewPlanner.Plan(
            Size,
            new Placement(40, 10, 0),
            Source(new Footprint(20, 20), new Placement(0, 11.5, 0))
        );

        // Act
        var overlaps = NeighborPreviewPlanner.Overlaps(north!, east!);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Plan_ClosesTheGateGapsOfTheNeighborsWalledEdges()
    {
        // Arrange
        var source = Source(
            new Footprint(20, 20),
            new Placement(5, 20, 0),
            otherExit: new BoundaryExit(
                Guid.NewGuid(),
                BoundaryExitKind.Wilderness,
                new Placement(10, 0, 0)
            )
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        Assert.Contains(
            neighbor!.Segments,
            segment =>
                segment.Placement.Y == -20
                && segment.Footprint == new Footprint(DistrictBoundaryPlanner.GateWidth, 0.4)
        );
    }

    [Fact]
    public void Plan_WallsAnotherEdgeThatLeadsToADistrict()
    {
        // Arrange
        var source = Source(
            new Footprint(20, 20),
            new Placement(5, 20, 0),
            otherExit: new BoundaryExit(
                Guid.NewGuid(),
                BoundaryExitKind.District,
                new Placement(0, 10, 0)
            )
        );

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        Assert.Contains(neighbor!.Segments, segment => segment.Placement.X == 5);
    }

    [Fact]
    public void Plan_ShiftsTheNeighborsProps()
    {
        // Arrange
        var prop = new ScenePropInfo(
            Guid.NewGuid(),
            "Crate",
            "A crate",
            "Furniture",
            IsOccupied: false,
            IsOccupiedByPlayer: false,
            Model: PropModel.ContainerCrate,
            Placement: new Placement(2, 3, 0),
            Footprint: new Footprint(1, 1)
        );
        var source = Source(new Footprint(20, 20), new Placement(5, 20, 0)) with { Props = [prop] };

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        var shifted = Assert.Single(neighbor!.Props);
        Assert.Equal(new Placement(7, -17, 0), shifted.Placement);
    }

    [Fact]
    public void Plan_ShiftsTheNeighborsRoads()
    {
        // Arrange
        var road = new DistrictRoad([new Point(5, 20), new Point(5, 10)], 4, RoadClass.Street);
        var source = Source(new Footprint(20, 20), new Placement(5, 20, 0)) with { Roads = [road] };

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 0, 0), source);

        // Assert
        var shifted = Assert.Single(neighbor!.Roads);
        Assert.Equal([new Point(10, 0), new Point(10, -10)], shifted.Points);
        Assert.Equal(road.Width, shifted.Width);
        Assert.Equal(road.Class, shifted.Class);
    }

    [Fact]
    public void Plan_ReturnsNothing_WhenTheExitIsNotOnAnEdge()
    {
        // Arrange
        var source = Source(new Footprint(20, 20), reverseExit: new Placement(5, 20, 0));

        // Act
        var neighbor = NeighborPreviewPlanner.Plan(Size, new Placement(10, 15, 0), source);

        // Assert
        Assert.Null(neighbor);
    }

    private static SceneNearbyBuildingInfo Building(Placement placement) =>
        new(
            Guid.NewGuid(),
            "House",
            BuildingType.House,
            placement,
            new Footprint(6, 5),
            FloorCount: 1
        );

    private static NeighborSource Source(
        Footprint size,
        Placement reverseExit,
        SceneNearbyBuildingInfo? building = null,
        BoundaryExit? otherExit = null
    ) =>
        new(
            Guid.NewGuid(),
            size,
            reverseExit,
            [
                new BoundaryExit(Guid.NewGuid(), BoundaryExitKind.District, reverseExit),
                .. otherExit is null ? [] : new[] { otherExit },
            ],
            [building ?? Building(new Placement(3, 4, 0))],
            [],
            [],
            []
        );
}
