using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Events;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Domain;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Subscriptions;

public sealed class SceneEventProjectorTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly WorldStateStamp Stamp = new(
        1,
        GameClock.Epoch,
        DateTimeOffset.UnixEpoch,
        1
    );

    private readonly SceneSubscriptions _subscriptions = new(
        Options.Create(new WorldSimulationOptions()),
        TimeProvider.System
    );
    private readonly SceneEventProjector _projector;

    public SceneEventProjectorTests() => _projector = new SceneEventProjector(_subscriptions);

    [Fact]
    public void Select_DropsDelta_WhenNobodyWatchesItsLocation()
    {
        // Arrange
        GameClientEvent[] events = [Delta(LocationId)];

        // Act
        var selected = _projector.Select(WorldId, events);

        // Assert
        Assert.Empty(selected);
    }

    [Fact]
    public void Select_KeepsDelta_WhenSnapshotInSameBatchRegistersTheWatch()
    {
        // Arrange
        var delta = Delta(LocationId);
        GameClientEvent[] events = [Snapshot(LocationId), delta];

        // Act
        var selected = _projector.Select(WorldId, events);

        // Assert
        Assert.Equal(2, selected.Count);
        Assert.Contains(delta, selected);
    }

    [Fact]
    public void Select_DropsDeltaForOldLocation_WhenSnapshotMovedThePlayer()
    {
        // Arrange
        var newLocationId = Guid.NewGuid();
        _projector.Select(WorldId, [Snapshot(LocationId)]);
        _projector.Select(WorldId, [Snapshot(newLocationId)]);
        GameClientEvent[] events = [Delta(LocationId), Delta(newLocationId)];

        // Act
        var selected = _projector.Select(WorldId, events);

        // Assert
        Assert.Equal([events[1]], selected);
    }

    [Fact]
    public void Select_KeepsEventsThatAreNotSceneEvents()
    {
        // Arrange
        GameClientEvent[] events = [new OtherEvent(WorldId)];

        // Act
        var selected = _projector.Select(WorldId, events);

        // Assert
        Assert.Equal(events, selected);
    }

    [Fact]
    public void RecordSendFailure_MarksWatchersOfTheDeltaLocationPending()
    {
        // Arrange
        _projector.Select(WorldId, [Snapshot(LocationId)]);

        // Act
        _projector.RecordSendFailure(Delta(LocationId));

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
    }

    private static SceneUpdatedEvent Snapshot(Guid locationId)
    {
        var scene = SceneResultBuilder.MakeScene();
        return new SceneUpdatedEvent(
            WorldId,
            scene with
            {
                LocationId = locationId,
                Player = scene.Player with { Id = PlayerId },
            },
            Stamp
        );
    }

    private static WeatherChangedEvent Delta(Guid locationId) =>
        new(WorldId, locationId, Stamp, WeatherCondition: null);

    private sealed record OtherEvent(Guid WorldId) : GameClientEvent(WorldId);
}
