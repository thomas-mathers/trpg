using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Clocks;
using TRPG.Domain;
using TRPG.Tests.Helpers;
using TRPG.Worlds;

namespace TRPG.Tests.Worlds;

public sealed class WorldClockCheckpointServiceTests : IAsyncLifetime
{
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );
    private readonly CountingWorldClock _worldClock = new();

    private WorldClockCheckpointService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _service = new WorldClockCheckpointService(
            _worldClock,
            _timeProvider,
            NullLogger<WorldClockCheckpointService>.Instance
        );

        await _service.StartAsync(TestContext.Current.CancellationToken);
        await WaitFor(() => _timeProvider.TimerCount == 1);
    }

    public ValueTask DisposeAsync()
    {
        _service.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Service_CheckpointsActiveWorldsEveryCadence()
    {
        // Act
        _timeProvider.Advance(WorldClockCheckpointService.Cadence);

        // Assert
        await WaitFor(() => _worldClock.CheckpointCount == 1);
        Assert.Equal(1, _worldClock.CheckpointCount);
    }

    [Fact]
    public async Task Service_RetriesOnNextTick_WhenCheckpointFails()
    {
        // Arrange
        _worldClock.FailCheckpoints = true;

        // Act
        _timeProvider.Advance(WorldClockCheckpointService.Cadence);
        await WaitFor(() => _worldClock.CheckpointCount == 1);
        _timeProvider.Advance(WorldClockCheckpointService.Cadence);

        // Assert
        await WaitFor(() => _worldClock.CheckpointCount == 2);
    }

    [Fact]
    public async Task Service_CheckpointsOnceMore_WhenStopped()
    {
        // Act
        await _service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, _worldClock.CheckpointCount);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var timeoutAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < timeoutAt, "The condition was not met in time.");
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    private sealed class CountingWorldClock : IWorldClock
    {
        private int _checkpointCount;

        public int CheckpointCount => Volatile.Read(ref _checkpointCount);
        public bool FailCheckpoints { get; set; }

        public IReadOnlyCollection<Guid> GetActiveWorldIds() => [];

        public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _checkpointCount);
            return FailCheckpoints
                ? throw new InvalidOperationException("The clock is unavailable.")
                : Task.CompletedTask;
        }

        public Task<GameInstant> GetCurrent(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> Checkpoint(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> ResumeWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> PauseWorld(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<GameInstant> Advance(
            Guid worldId,
            TimeSpan duration,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
