using Microsoft.Extensions.Options;
using TRPG.Application.Scenes.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Subscriptions;

public sealed class SceneSubscriptionsTests
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private readonly ManualTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private readonly SceneSubscriptions _subscriptions;

    public SceneSubscriptionsTests()
    {
        _subscriptions = new SceneSubscriptions(
            Options.Create(
                new WorldSimulationOptions
                {
                    ResyncAttempts = 3,
                    ResyncWindow = TimeSpan.FromSeconds(6),
                }
            ),
            _time
        );
        _subscriptions.Watch(WorldId, PlayerId, LocationId);
    }

    [Fact]
    public void MarkPlayerForResync_MakesWatchPending_WhenSynced()
    {
        // Act
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void TakePending_ReturnsPlayerAndMarksSent_WhenWatchIsPending()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Act
        var pending = _subscriptions.TakePending(WorldId);

        // Assert
        Assert.Equal([PlayerId], pending);
        Assert.Equal(ResyncState.Sent, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void CompleteResync_MakesWatchSynced_WhenSent()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);

        // Act
        _subscriptions.CompleteResync(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Synced, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void MarkLocationForResync_MakesOnlyWatchersOfThatLocationPending()
    {
        // Arrange
        var bystanderId = Guid.NewGuid();
        _subscriptions.Watch(WorldId, bystanderId, Guid.NewGuid());

        // Act
        _subscriptions.MarkLocationForResync(WorldId, LocationId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
        Assert.Equal(ResyncState.Synced, _subscriptions.StateOf(WorldId, bystanderId));
    }

    [Fact]
    public void MarkWorldForResync_LeavesAnInFlightResyncAlone_WhenAlreadyPending()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Act
        _subscriptions.MarkWorldForResync(WorldId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
        Assert.Empty(_subscriptions.TakeExhausted(WorldId));
    }

    [Fact]
    public void MarkPlayerForResync_ExhaustsWatch_WhenFailuresReachTheAttemptLimitInsideTheWindow()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Act
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Exhausted, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void MarkPlayerForResync_KeepsWatchPending_WhenFailuresAreSpreadAcrossWindows()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _time.Advance(TimeSpan.FromSeconds(7));

        // Act
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void TakeExhausted_RemovesWatchAndReturnsPlayer_WhenExhausted()
    {
        // Arrange
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);
        _subscriptions.MarkPlayerForResync(WorldId, PlayerId);

        // Act
        var exhausted = _subscriptions.TakeExhausted(WorldId);

        // Assert
        Assert.Equal([PlayerId], exhausted);
        Assert.Null(_subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void Watch_MovesWatchToNewLocation_WhenPlayerAlreadyWatched()
    {
        // Arrange
        var newLocationId = Guid.NewGuid();

        // Act
        _subscriptions.Watch(WorldId, PlayerId, newLocationId);

        // Assert
        Assert.False(_subscriptions.IsWatchingLocation(WorldId, LocationId));
        Assert.True(_subscriptions.IsWatchingLocation(WorldId, newLocationId));
    }

    [Fact]
    public void LocationOf_ReturnsWatchedLocation_WhenPlayerIsWatched()
    {
        // Act
        var location = _subscriptions.LocationOf(WorldId, PlayerId);

        // Assert
        Assert.Equal(LocationId, location);
    }

    [Fact]
    public void LocationOf_ReturnsNull_WhenPlayerIsNotWatched()
    {
        // Act
        var location = _subscriptions.LocationOf(WorldId, Guid.NewGuid());

        // Assert
        Assert.Null(location);
    }

    [Fact]
    public void RequestRefresh_MakesWatchPendingWithoutCountingAFailure()
    {
        // Arrange
        _subscriptions.RequestRefresh(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);
        _subscriptions.CompleteResync(WorldId, PlayerId);

        // Act
        _subscriptions.RequestRefresh(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);
        _subscriptions.RequestRefresh(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);
        _subscriptions.RequestRefresh(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);
        _subscriptions.RequestRefresh(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void RequestRefresh_KeepsTheWatchPending_WhenASnapshotIsAlreadyInFlight()
    {
        // Arrange
        _subscriptions.RequestRefresh(WorldId, PlayerId);
        _subscriptions.TakePending(WorldId);
        _subscriptions.RequestRefresh(WorldId, PlayerId);

        // Act
        _subscriptions.CompleteResync(WorldId, PlayerId);

        // Assert
        Assert.Equal(ResyncState.Pending, _subscriptions.StateOf(WorldId, PlayerId));
    }

    [Fact]
    public void RequestLocationRefresh_MakesOnlyWatchersOfThatLocationPending()
    {
        // Arrange
        var bystanderId = Guid.NewGuid();
        _subscriptions.Watch(WorldId, bystanderId, Guid.NewGuid());

        // Act
        _subscriptions.RequestLocationRefresh(WorldId, LocationId);

        // Assert
        Assert.Equal([PlayerId], _subscriptions.TakePending(WorldId));
    }

    [Fact]
    public void RecordCreatureMembership_AddsArrivedAndRemovesLeft()
    {
        // Arrange
        var stayed = Guid.NewGuid();
        var left = Guid.NewGuid();
        var arrived = Guid.NewGuid();
        _subscriptions.RecordCreatureMembership(WorldId, PlayerId, [stayed, left], []);

        // Act
        _subscriptions.RecordCreatureMembership(WorldId, PlayerId, [arrived], [left]);

        // Assert
        var watch = _subscriptions.SnapshotOf(WorldId, PlayerId);
        Assert.Equivalent(new[] { stayed, arrived }, watch!.KnownCreatureIds);
    }

    [Fact]
    public void RecordSeats_ReplacesTheRecordedOccupancy()
    {
        // Arrange
        var seatId = Guid.NewGuid();
        _subscriptions.RecordSeats(WorldId, PlayerId, [new PropOccupancy(seatId, true, false)]);

        // Act
        _subscriptions.RecordSeats(WorldId, PlayerId, [new PropOccupancy(seatId, false, false)]);

        // Assert
        var watch = _subscriptions.SnapshotOf(WorldId, PlayerId);
        Assert.Equal(new PropOccupancy(seatId, false, false), watch!.Seats[seatId]);
    }

    [Fact]
    public void RecordPlayerSeat_MovesThePlayerFromTheOldSeatToTheNewOne()
    {
        // Arrange
        var oldSeatId = Guid.NewGuid();
        var newSeatId = Guid.NewGuid();
        _subscriptions.RecordSeats(WorldId, PlayerId, [new PropOccupancy(oldSeatId, true, true)]);

        // Act
        _subscriptions.RecordPlayerSeat(WorldId, PlayerId, newSeatId);

        // Assert
        var watch = _subscriptions.SnapshotOf(WorldId, PlayerId);
        Assert.Equal(new PropOccupancy(oldSeatId, false, false), watch!.Seats[oldSeatId]);
        Assert.Equal(new PropOccupancy(newSeatId, true, true), watch.Seats[newSeatId]);
    }

    [Fact]
    public void RecordWeather_StoresTheAnnouncedWeather()
    {
        // Act
        _subscriptions.RecordWeather(WorldId, PlayerId, WeatherCondition.Storm);

        // Assert
        Assert.Equal(WeatherCondition.Storm, _subscriptions.SnapshotOf(WorldId, PlayerId)!.Weather);
    }

    [Fact]
    public void Watch_ForgetsWhatTheClientKnew_WhenTheLocationChanges()
    {
        // Arrange
        _subscriptions.RecordCreatureMembership(WorldId, PlayerId, [Guid.NewGuid()], []);
        _subscriptions.RecordWeather(WorldId, PlayerId, WeatherCondition.Rain);
        var newLocationId = Guid.NewGuid();

        // Act
        _subscriptions.Watch(WorldId, PlayerId, newLocationId);

        // Assert
        var watch = _subscriptions.SnapshotOf(WorldId, PlayerId);
        Assert.Equal(newLocationId, watch!.LocationId);
        Assert.Empty(watch.KnownCreatureIds);
        Assert.Null(watch.Weather);
    }

    [Fact]
    public void Watch_KeepsWhatTheClientKnew_WhenTheLocationIsUnchanged()
    {
        // Arrange
        var known = Guid.NewGuid();
        _subscriptions.RecordCreatureMembership(WorldId, PlayerId, [known], []);

        // Act
        _subscriptions.Watch(WorldId, PlayerId, LocationId);

        // Assert
        Assert.Equal([known], _subscriptions.SnapshotOf(WorldId, PlayerId)!.KnownCreatureIds);
    }

    [Fact]
    public void SnapshotOf_ReturnsNull_WhenThePlayerIsNotWatched()
    {
        // Act
        var watch = _subscriptions.SnapshotOf(WorldId, Guid.NewGuid());

        // Assert
        Assert.Null(watch);
    }
}
