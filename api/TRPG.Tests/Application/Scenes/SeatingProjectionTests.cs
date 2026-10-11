using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes;

public sealed class SeatingProjectionTests
{
    private static readonly WorldStateStamp Stamp = new(
        1,
        GameClock.Epoch,
        DateTimeOffset.UnixEpoch,
        1
    );

    private readonly ScenePropInfo _seat = MakeSeat();
    private readonly ScenePropInfo _otherSeat = MakeSeat();
    private readonly SceneResult _scene;

    public SeatingProjectionTests()
    {
        var scene = SceneResultBuilder.MakeScene(others: ["Bystander"]);
        _scene = scene with { NearbyProps = [_seat, _otherSeat] };
    }

    [Fact]
    public void Apply_OccupiesTheSeatForThePlayerAndMovesThem_WhenThePlayerSits()
    {
        // Arrange
        var sit = Seating(_scene.Player.Id, _seat.Id, CreaturePosture.Sitting);

        // Act
        var result = SeatingProjection.Apply(_scene, sit);

        // Assert
        Assert.Equal(CreaturePosture.Sitting, result.Player.Posture);
        Assert.Equal(sit.Placement, result.Player.Placement);
        var occupied = Assert.Single(result.NearbyProps, prop => prop.Id == _seat.Id);
        Assert.True(occupied.IsOccupied);
        Assert.True(occupied.IsOccupiedByPlayer);
        Assert.Equal(
            _otherSeat,
            Assert.Single(result.NearbyProps, prop => prop.Id == _otherSeat.Id)
        );
    }

    [Fact]
    public void Apply_ReleasesThePlayersSeat_WhenThePlayerStands()
    {
        // Arrange
        var seated = _scene with
        {
            NearbyProps = [_seat with { IsOccupied = true, IsOccupiedByPlayer = true }, _otherSeat],
        };
        var stand = Seating(_scene.Player.Id, seatId: null, CreaturePosture.Standing);

        // Act
        var result = SeatingProjection.Apply(seated, stand);

        // Assert
        Assert.Equal(CreaturePosture.Standing, result.Player.Posture);
        Assert.All(result.NearbyProps, prop => Assert.False(prop.IsOccupied));
    }

    [Fact]
    public void Apply_OccupiesTheSeatWithoutClaimingItForThePlayer_WhenAnotherCreatureSits()
    {
        // Arrange
        var bystander = _scene.NearbyCreatures.Single();
        var sit = Seating(bystander.Id, _seat.Id, CreaturePosture.Sitting);

        // Act
        var result = SeatingProjection.Apply(_scene, sit);

        // Assert
        var occupied = Assert.Single(result.NearbyProps, prop => prop.Id == _seat.Id);
        Assert.True(occupied.IsOccupied);
        Assert.False(occupied.IsOccupiedByPlayer);
        Assert.Equal(CreaturePosture.Sitting, result.NearbyCreatures.Single().Posture);
        Assert.Equal(_scene.Player, result.Player);
    }

    private SeatingChangedEvent Seating(Guid creatureId, Guid? seatId, CreaturePosture posture) =>
        new(
            _scene.WorldId,
            _scene.LocationId,
            Stamp,
            creatureId,
            seatId,
            posture,
            new Placement(4, 5, 1.5)
        );

    private static ScenePropInfo MakeSeat() =>
        new(
            Guid.NewGuid(),
            "Chair",
            "A chair.",
            ScenePropInfo.FurnitureType,
            IsOccupied: false,
            IsOccupiedByPlayer: false,
            PropModel.SeatChair,
            new Placement(0, 0, 0),
            new Footprint(1, 1),
            BlocksMovement: false
        );
}
