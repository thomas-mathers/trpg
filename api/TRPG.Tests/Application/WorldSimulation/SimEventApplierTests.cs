using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Persistence;
using TRPG.Domain;

namespace TRPG.Tests.Application.WorldSimulation;

public sealed class SimEventApplierTests
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));
    private const int MaximumConsecutiveFailures = 3;

    private readonly FakeStore _store = new();
    private readonly SimEventApplier _applier;

    public SimEventApplierTests()
    {
        _applier = new SimEventApplier(_store.Write, _store.ReadMarker, MaximumConsecutiveFailures);
    }

    [Fact]
    public async Task Commit_ReturnsAndClearsTheEnqueuedEvents_WhenTheWriteSucceeds()
    {
        // Arrange
        var simEvent = MakeEvent();
        _applier.Enqueue([simEvent]);

        // Act
        var committed = await _applier.Commit(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([simEvent], committed);
        Assert.False(_applier.HasPending);
        Assert.Equal(1, _store.WriteCount);
    }

    [Fact]
    public async Task Commit_KeepsTheEventsPending_WhenTheWriteFails()
    {
        // Arrange
        _applier.Enqueue([MakeEvent()]);
        _store.FailBeforeCommit = true;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.True(_applier.HasPending);
        Assert.True(_applier.IsBlocked);
    }

    [Fact]
    public async Task Commit_WritesTheSameBatchAgain_WhenTheFailedWriteDidNotCommit()
    {
        // Arrange
        var simEvent = MakeEvent();
        _applier.Enqueue([simEvent]);
        _store.FailBeforeCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );
        _store.FailBeforeCommit = false;

        // Act
        var committed = await _applier.Commit(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([simEvent], committed);
        Assert.Equal(2, _store.WriteCount);
        Assert.False(_applier.IsBlocked);
    }

    [Fact]
    public async Task Commit_DoesNotWriteAgain_WhenTheFailedWriteHadCommitted()
    {
        // Arrange
        var simEvent = MakeEvent();
        _applier.Enqueue([simEvent]);
        _store.FailAfterCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );
        _store.FailAfterCommit = false;

        // Act
        var committed = await _applier.Commit(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([simEvent], committed);
        Assert.Equal(1, _store.WriteCount);
        Assert.False(_applier.HasPending);
    }

    [Fact]
    public async Task RetriesExhausted_IsTrue_AfterTheMaximumConsecutiveFailures()
    {
        // Arrange
        _applier.Enqueue([MakeEvent()]);
        _store.FailBeforeCommit = true;
        for (var attempt = 0; attempt < MaximumConsecutiveFailures; attempt++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _applier.Commit(TestContext.Current.CancellationToken)
            );
        }

        // Act
        var exhausted = _applier.RetriesExhausted;

        // Assert
        Assert.True(exhausted);
    }

    [Fact]
    public async Task RetriesExhausted_IsFalse_BeforeTheMaximumConsecutiveFailures()
    {
        // Arrange
        _applier.Enqueue([MakeEvent()]);
        _store.FailBeforeCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );

        // Act
        var exhausted = _applier.RetriesExhausted;

        // Assert
        Assert.False(exhausted);
    }

    [Fact]
    public async Task ResolveUncertain_ReturnsTheBatch_WhenTheFailedWriteHadCommitted()
    {
        // Arrange
        var simEvent = MakeEvent();
        _applier.Enqueue([simEvent]);
        _store.FailAfterCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );

        // Act
        var committed = await _applier.ResolveUncertain(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([simEvent], committed);
        Assert.False(_applier.HasPending);
    }

    [Fact]
    public async Task ResolveUncertain_DiscardsTheBatch_WhenTheFailedWriteDidNotCommit()
    {
        // Arrange
        _applier.Enqueue([MakeEvent()]);
        _store.FailBeforeCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _applier.Commit(TestContext.Current.CancellationToken)
        );

        // Act
        var committed = await _applier.ResolveUncertain(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(committed);
        Assert.False(_applier.HasPending);
        Assert.False(_applier.IsBlocked);
    }

    [Fact]
    public async Task ResolveUncertain_DiscardsEventsThatWereNeverWritten()
    {
        // Arrange
        _applier.Enqueue([MakeEvent()]);

        // Act
        var committed = await _applier.ResolveUncertain(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(committed);
        Assert.False(_applier.HasPending);
        Assert.Equal(0, _store.WriteCount);
    }

    private static JourneyCheckpoint MakeEvent() =>
        new(
            CreatureId: Guid.NewGuid(),
            At: Now,
            LocationId: Guid.NewGuid(),
            JourneyId: Guid.NewGuid(),
            Status: TRPG.Domain.Models.JourneyStatus.Traveling,
            LegIndex: 0,
            LegProgressMeters: 0
        );

    private sealed class FakeStore
    {
        private long _marker;

        public int WriteCount { get; private set; }
        public bool FailBeforeCommit { get; set; }
        public bool FailAfterCommit { get; set; }

        public Task Write(IReadOnlyList<SimEvent> events, CancellationToken cancellationToken)
        {
            WriteCount++;
            if (FailBeforeCommit)
            {
                throw new InvalidOperationException("write failed");
            }

            _marker++;
            if (FailAfterCommit)
            {
                throw new InvalidOperationException("connection lost after commit");
            }

            return Task.CompletedTask;
        }

        public Task<long> ReadMarker(CancellationToken cancellationToken) =>
            Task.FromResult(_marker);
    }
}
