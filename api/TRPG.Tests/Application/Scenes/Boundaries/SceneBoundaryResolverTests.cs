using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Boundaries;

public class SceneBoundaryResolverTests
{
    private static readonly Footprint Size = new(40, 30);

    [Fact]
    public void Resolve_OpensTheEdge_ForADistrictExit()
    {
        // Arrange
        SceneExitInfo[] exits =
        [
            ExitTo(new SceneDistrictExitDestination("Market", DistrictType.CityCenter), 20, 0),
        ];

        // Act
        var boundary = SceneBoundaryResolver.Resolve(Size, exits, []);

        // Assert
        Assert.Equal([CompassDirection.North], boundary.OpenEdges);
    }

    [Fact]
    public void Resolve_CutsAGate_ForAWildernessExit()
    {
        // Arrange
        var gate = ExitTo(new SceneWildernessExitDestination("Wilderness"), 20, 30);

        // Act
        var boundary = SceneBoundaryResolver.Resolve(Size, [gate], []);

        // Assert
        Assert.Equal(gate.ConnectorId, Assert.Single(boundary.Gates).ConnectorId);
    }

    [Fact]
    public void Resolve_IgnoresBuildingDoors_EvenOnTheEdge()
    {
        // Arrange
        SceneExitInfo[] exits =
        [
            ExitTo(new SceneBuildingExitDestination("Tavern", BuildingType.Tavern), 20, 30),
        ];

        // Act
        var boundary = SceneBoundaryResolver.Resolve(Size, exits, []);

        // Assert
        Assert.Empty(boundary.OpenEdges);
        Assert.Empty(boundary.Gates);
    }

    [Fact]
    public void Resolve_WallsTheStretchOfTheEdgeTheNeighborDoesNotCover()
    {
        // Arrange
        var forgeWardId = Guid.NewGuid();
        var merchantQuarter = new Footprint(95, 75);
        var toForgeWard = ExitTo(
            new SceneDistrictExitDestination("Forge Ward", DistrictType.Residential),
            0,
            37.5,
            forgeWardId
        );
        var forgeWard = new NeighborDistrict(
            forgeWardId,
            new Placement(-82, 4.5, 0),
            new Footprint(82, 66),
            [],
            [],
            [],
            []
        );

        // Act
        var boundary = SceneBoundaryResolver.Resolve(merchantQuarter, [toForgeWard], [forgeWard]);

        // Assert
        Assert.Equal([CompassDirection.West], boundary.OpenEdges);
        var walls = boundary
            .Segments.Where(segment => segment.Placement.X == 0)
            .OrderBy(segment => segment.Placement.Y)
            .ToArray();
        Assert.Equal(2, walls.Length);
        Assert.Equal(4.5, walls[0].Placement.Y + walls[0].Footprint.Depth / 2, 6);
        Assert.Equal(70.5, walls[1].Placement.Y - walls[1].Footprint.Depth / 2, 6);
    }

    private static SceneExitInfo ExitTo(
        SceneExitDestination destination,
        double x,
        double y,
        Guid? destinationLocationId = null
    ) =>
        new(
            Guid.NewGuid(),
            "A path.",
            destination,
            IsLocked: false,
            Direction: null,
            IsVisited: false,
            IsWayBack: false,
            DestinationLocationId: destinationLocationId ?? Guid.NewGuid(),
            Placement: new Placement(x, y, 0)
        );
}
