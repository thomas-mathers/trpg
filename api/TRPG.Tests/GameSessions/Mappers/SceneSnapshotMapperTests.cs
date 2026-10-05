using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Mappers;
using TRPG.Tests.Helpers;
using WireBoundarySegmentKind = TRPG.GameSessions.Responses.BoundarySegmentKind;
using WireCompassDirection = TRPG.GameSessions.Responses.CompassDirection;
using WireLocationBoundary = TRPG.GameSessions.Responses.LocationBoundarySnapshot;
using WireRoadClass = TRPG.GameSessions.Responses.RoadClassSnapshot;

namespace TRPG.Tests.GameSessions.Mappers;

public sealed class SceneSnapshotMapperTests
{
    [Fact]
    public void ToSnapshot_AttachesSpatialDataToItsEntity()
    {
        var source = SceneResultBuilder.MakeScene();
        var propId = Guid.NewGuid();
        var buildingId = Guid.NewGuid();
        var scene = source with
        {
            NearbyProps =
            [
                new ScenePropInfo(
                    propId,
                    "Chair",
                    "A chair.",
                    "Seat",
                    false,
                    false,
                    PropModel.SeatChair,
                    new Placement(1, 2, 0.5),
                    new Footprint(2, 1)
                ),
            ],
            NearbyBuildings =
            [
                new SceneNearbyBuildingInfo(
                    buildingId,
                    "Inn",
                    BuildingType.Inn,
                    new Placement(20, 8, 1),
                    new Footprint(6, 4),
                    FloorCount: 3
                ),
            ],
            Exits = [SceneResultBuilder.MakeExit("Inn", isLocked: false)],
            Player = source.Player with { Placement = new Placement(5, 6, 1.5) },
            Size = new Footprint(40, 30),
        };

        var snapshot = scene.ToSnapshot(
            new WorldStateStamp(3, GameClock.Epoch, DateTimeOffset.UnixEpoch, 1)
        );

        Assert.Equal(scene.LocationId, snapshot.LocationId);
        Assert.Equal(40, snapshot.Size.Width);
        Assert.Equal(1.5, snapshot.PlayerStatus.Placement.Angle);
        Assert.Equal(0.5, Assert.Single(snapshot.NearbyProps).Placement.Angle);
        Assert.Equal(6, Assert.Single(snapshot.NearbyBuildings).Footprint.Width);
        Assert.Equal(3, Assert.Single(snapshot.NearbyBuildings).FloorCount);
        Assert.Equal(
            scene.Exits.Single().Placement.Angle,
            Assert.Single(snapshot.Exits).Placement.Angle
        );
    }

    [Fact]
    public void ToSnapshot_OmitsTheBoundary_WhenTheSceneHasNone()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene() with
        {
            Boundary = null,
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        Assert.Null(snapshot.Boundary);
    }

    [Fact]
    public void ToSnapshot_CarriesTheBoundaryWallsGatesAndOpenEdges()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        var scene = SceneResultBuilder.MakeScene() with
        {
            Boundary = new DistrictBoundary(
                Segments:
                [
                    new BoundarySegment(
                        BoundarySegmentKind.Tower,
                        new Placement(18, 30, 0),
                        new Footprint(1.6, 1.6)
                    ),
                ],
                Gates: [new BoundaryGate(connectorId, new Placement(20, 30, Math.PI), 4)],
                OpenEdges: [CompassDirection.North]
            ),
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        var boundary = Assert.IsType<WireLocationBoundary>(snapshot.Boundary);
        var segment = Assert.Single(boundary.Segments);
        Assert.Equal(WireBoundarySegmentKind.Tower, segment.Kind);
        Assert.Equal(18, segment.Placement.X);
        Assert.Equal(1.6, segment.Footprint.Width);
        var gate = Assert.Single(boundary.Gates);
        Assert.Equal(connectorId, gate.ConnectorId);
        Assert.Equal(4, gate.Width);
        Assert.Equal([WireCompassDirection.North], boundary.OpenEdges);
    }

    [Fact]
    public void ToSnapshot_OmitsTheRoads_WhenTheSceneHasNone()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene() with
        {
            Roads = null,
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        Assert.Null(snapshot.Roads);
    }

    [Fact]
    public void ToSnapshot_CarriesEachRoadPolylineAndWidth()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene() with
        {
            Roads = [new DistrictRoad([new Point(1, 2), new Point(3, 4)], 2.5, RoadClass.Street)],
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        var road = Assert.Single(snapshot.Roads!);
        Assert.Equal(2.5, road.Width);
        Assert.Equal(WireRoadClass.Street, road.Class);
        Assert.Equal([2.0, 4.0], road.Points.Select(point => point.Y));
        Assert.Equal([1.0, 3.0], road.Points.Select(point => point.X));
    }

    [Fact]
    public void ToSnapshot_OmitsTheNeighbors_WhenTheSceneHasNone()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene() with
        {
            Neighbors = null,
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        Assert.Null(snapshot.Neighbors);
    }

    [Fact]
    public void ToSnapshot_CarriesEachNeighborsBuildingsAndWalls()
    {
        // Arrange
        var neighborId = Guid.NewGuid();
        var scene = SceneResultBuilder.MakeScene() with
        {
            Neighbors =
            [
                new NeighborDistrict(
                    neighborId,
                    new Placement(0, -20, 0),
                    new Footprint(20, 20),
                    [
                        new SceneNearbyBuildingInfo(
                            Guid.NewGuid(),
                            "House",
                            BuildingType.House,
                            new Placement(8, -16, 0),
                            new Footprint(6, 5),
                            FloorCount: 2
                        ),
                    ],
                    [
                        new ScenePropInfo(
                            Guid.NewGuid(),
                            "Crate",
                            "A crate",
                            "Furniture",
                            IsOccupied: false,
                            IsOccupiedByPlayer: false,
                            Model: PropModel.ContainerCrate,
                            Placement: new Placement(9, -3, 0),
                            Footprint: new Footprint(1, 1)
                        ),
                    ],
                    [
                        new BoundarySegment(
                            BoundarySegmentKind.Wall,
                            new Placement(10, -20, 0),
                            new Footprint(20, 0.4)
                        ),
                    ],
                    [new DistrictRoad([new Point(10, -20), new Point(10, -5)], 4, RoadClass.Street)]
                ),
            ],
        };

        // Act
        var snapshot = scene.ToSnapshot(Stamp);

        // Assert
        var neighbor = Assert.Single(snapshot.Neighbors!);
        Assert.Equal(neighborId, neighbor.LocationId);
        Assert.Equal(-16, Assert.Single(neighbor.Buildings).Placement.Y);
        Assert.Equal(-3, Assert.Single(neighbor.Props).Placement.Y);
        Assert.Equal(WireBoundarySegmentKind.Wall, Assert.Single(neighbor.Segments).Kind);
        Assert.Equal(-20, Assert.Single(neighbor.Roads).Points.First().Y);
    }

    private static readonly WorldStateStamp Stamp = new(
        3,
        GameClock.Epoch,
        DateTimeOffset.UnixEpoch,
        1
    );
}
