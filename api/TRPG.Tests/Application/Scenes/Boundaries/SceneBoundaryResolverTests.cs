using TRPG.Application.Scenes.Boundaries;
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
        var boundary = SceneBoundaryResolver.Resolve(Size, exits);

        // Assert
        Assert.Equal([CompassDirection.North], boundary.OpenEdges);
    }

    [Fact]
    public void Resolve_CutsAGate_ForAWildernessExit()
    {
        // Arrange
        var gate = ExitTo(new SceneWildernessExitDestination("Wilderness"), 20, 30);

        // Act
        var boundary = SceneBoundaryResolver.Resolve(Size, [gate]);

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
        var boundary = SceneBoundaryResolver.Resolve(Size, exits);

        // Assert
        Assert.Empty(boundary.OpenEdges);
        Assert.Empty(boundary.Gates);
    }

    private static SceneExitInfo ExitTo(SceneExitDestination destination, double x, double y) =>
        new(
            Guid.NewGuid(),
            "A path.",
            destination,
            IsLocked: false,
            Direction: null,
            IsVisited: false,
            IsWayBack: false,
            DestinationLocationId: Guid.NewGuid(),
            Placement: new Placement(x, y, 0)
        );
}
