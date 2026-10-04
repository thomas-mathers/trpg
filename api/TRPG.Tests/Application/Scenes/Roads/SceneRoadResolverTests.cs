using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Scenes.Roads;

public class SceneRoadResolverTests
{
    private static readonly Footprint Size = new(40, 30);

    [Fact]
    public void Resolve_StartsARoadAtEachBuildingDoorDistrictExitAndGate()
    {
        // Arrange
        SceneExitInfo[] exits =
        [
            ExitTo(new SceneBuildingExitDestination("Tavern", BuildingType.Tavern), 10, 10, 0),
            ExitTo(
                new SceneDistrictExitDestination("Market", DistrictType.CityCenter),
                20,
                0,
                Math.PI
            ),
            ExitTo(new SceneWildernessExitDestination("Wilderness"), 20, 30, 0),
        ];

        // Act
        var roads = SceneRoadResolver.Resolve(Size, [], exits);

        // Assert
        Assert.Equal(
            [new Point(10, 10), new Point(20, 0), new Point(20, 30)],
            roads.Select(road => road.Points[0]).OrderBy(point => point.X).ThenBy(point => point.Y)
        );
    }

    [Fact]
    public void Resolve_IgnoresExitsThatLeadInsideABuilding()
    {
        // Arrange
        SceneExitInfo[] exits =
        [
            ExitTo(
                new SceneRoomExitDestination("Cellar", BuildingType.Tavern, Role: null),
                5,
                5,
                0
            ),
        ];

        // Act
        var roads = SceneRoadResolver.Resolve(Size, [], exits);

        // Assert
        Assert.Empty(roads);
    }

    [Fact]
    public void Resolve_RoutesRoadsAroundTheNearbyBuildings()
    {
        // Arrange
        SceneNearbyBuildingInfo[] buildings =
        [
            new(
                Guid.NewGuid(),
                "Tavern",
                BuildingType.Tavern,
                new Placement(20, 15, 0),
                new Footprint(8, 6),
                FloorCount: 1
            ),
        ];
        SceneExitInfo[] exits =
        [
            ExitTo(
                new SceneDistrictExitDestination("Market", DistrictType.CityCenter),
                20,
                0,
                Math.PI
            ),
            ExitTo(new SceneWildernessExitDestination("Wilderness"), 20, 30, 0),
        ];

        // Act
        var roads = SceneRoadResolver.Resolve(Size, buildings, exits);

        // Assert
        Assert.All(
            roads.SelectMany(road => road.Points),
            point => Assert.False(Math.Abs(point.X - 20) < 4 && Math.Abs(point.Y - 15) < 3)
        );
    }

    private static SceneExitInfo ExitTo(
        SceneExitDestination destination,
        double x,
        double y,
        double angle
    ) =>
        new(
            Guid.NewGuid(),
            "A path.",
            destination,
            IsLocked: false,
            Direction: null,
            IsVisited: false,
            IsWayBack: false,
            DestinationLocationId: Guid.NewGuid(),
            Placement: new Placement(x, y, angle)
        );
}
