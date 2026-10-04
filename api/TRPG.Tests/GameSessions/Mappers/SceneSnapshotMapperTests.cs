using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Mappers;
using TRPG.Tests.Helpers;

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
}
