using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes;

public sealed class PropOccupancyProjectionTests
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

    public PropOccupancyProjectionTests()
    {
        var scene = SceneResultBuilder.MakeScene();
        _scene = scene with { NearbyProps = [_seat, _otherSeat] };
    }

    [Fact]
    public void Diff_ReportsOnlyTheSeatThatChanged_WhenAnotherCreatureSitsInOne()
    {
        // Arrange
        var occupants = new Dictionary<Guid, Guid?>
        {
            [_seat.Id] = Guid.NewGuid(),
            [_otherSeat.Id] = null,
        };

        // Act
        var changes = PropOccupancyProjection.Diff(_scene, occupants);

        // Assert
        var change = Assert.Single(changes);
        Assert.Equal(new PropOccupancy(_seat.Id, true, false), change);
    }

    [Fact]
    public void Diff_MarksTheSeatAsThePlayers_WhenThePlayerIsTheOccupant()
    {
        // Arrange
        var occupants = new Dictionary<Guid, Guid?> { [_seat.Id] = _scene.Player.Id };

        // Act
        var changes = PropOccupancyProjection.Diff(_scene, occupants);

        // Assert
        Assert.Equal(new PropOccupancy(_seat.Id, true, true), Assert.Single(changes));
    }

    [Fact]
    public void Diff_ReportsNothing_WhenOccupancyMatchesTheScene()
    {
        // Arrange
        var occupants = new Dictionary<Guid, Guid?> { [_seat.Id] = null, [_otherSeat.Id] = null };

        // Act
        var changes = PropOccupancyProjection.Diff(_scene, occupants);

        // Assert
        Assert.Empty(changes);
    }

    [Fact]
    public void Diff_IgnoresPropsThatAreNotSeats_WhenTheyHaveNoOccupantEntry()
    {
        // Arrange
        var occupants = new Dictionary<Guid, Guid?>();

        // Act
        var changes = PropOccupancyProjection.Diff(_scene, occupants);

        // Assert
        Assert.Empty(changes);
    }

    [Fact]
    public void Apply_UpdatesOnlyTheChangedProps_WhenAnOccupancyChanges()
    {
        // Arrange
        var change = new PropOccupancyChangedEvent(
            _scene.WorldId,
            _scene.LocationId,
            Stamp,
            [new PropOccupancy(_seat.Id, true, false)]
        );

        // Act
        var result = PropOccupancyProjection.Apply(_scene, change);

        // Assert
        Assert.True(Assert.Single(result.NearbyProps, prop => prop.Id == _seat.Id).IsOccupied);
        Assert.Equal(
            _otherSeat,
            Assert.Single(result.NearbyProps, prop => prop.Id == _otherSeat.Id)
        );
    }

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
