using Microsoft.Extensions.Options;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Subscriptions;
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
}
