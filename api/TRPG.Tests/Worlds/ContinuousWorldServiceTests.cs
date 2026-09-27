using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Tests.Helpers;
using TRPG.Worlds;

namespace TRPG.Tests.Worlds;

public sealed class ContinuousWorldServiceTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CountingWorldClock _worldClock = null!;
    private ContinuousWorldService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithScopedDbContexts(db.ConnectionString)
            .BuildServiceProvider();

        var world = Builders.MakeWorld();
        _context.Worlds.Add(world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldClock = new CountingWorldClock(world.Id);
        var processor = new ContinuousWorldProcessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _worldClock,
            new WorldMutationGate(),
            NullLogger<ContinuousWorldProcessor>.Instance
        );
        _service = new ContinuousWorldService(
            processor,
            _timeProvider,
            NullLogger<ContinuousWorldService>.Instance
        );

        await _service.StartAsync(TestContext.Current.CancellationToken);
        await WaitFor(() => _timeProvider.TimerCount == 2);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.StopAsync(TestContext.Current.CancellationToken);
        _service.Dispose();
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Service_RunsFrequentPassEveryFiveSeconds_WithoutRunningRoutinePass()
    {
        // Act
        _timeProvider.Advance(ContinuousWorldService.FrequentCadence);

        // Assert
        await WaitFor(() => _worldClock.CheckpointCount == 1);
        await WaitFor(() => _worldClock.CurrentCount == 1);
        Assert.Equal(1, _worldClock.CheckpointCount);
        Assert.Equal(1, _worldClock.CurrentCount);
    }

    [Fact]
    public async Task Service_RunsRoutinePassEveryThirtySeconds()
    {
        // Act
        for (var pass = 1; pass <= 6; pass++)
        {
            _timeProvider.Advance(ContinuousWorldService.FrequentCadence);
            var expectedPasses = pass;
            await WaitFor(() => _worldClock.CheckpointCount == expectedPasses);
        }

        // Assert
        await WaitFor(() => _worldClock.CurrentCount == 7);
        Assert.Equal(6, _worldClock.CheckpointCount);
        Assert.Equal(7, _worldClock.CurrentCount);
    }

    [Fact]
    public async Task Service_RetriesOnNextTick_WhenPassFails()
    {
        // Arrange
        _worldClock.FailCheckpoints = true;

        // Act
        _timeProvider.Advance(ContinuousWorldService.FrequentCadence);
        await WaitFor(() => _worldClock.CheckpointCount == 1);
        _timeProvider.Advance(ContinuousWorldService.FrequentCadence);

        // Assert
        await WaitFor(() => _worldClock.CheckpointCount == 2);
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

    private sealed class CountingWorldClock(Guid activeWorldId) : IWorldClock
    {
        private int _checkpointCount;
        private int _currentCount;

        public int CheckpointCount => Volatile.Read(ref _checkpointCount);
        public int CurrentCount => Volatile.Read(ref _currentCount);
        public bool FailCheckpoints { get; set; }

        public IReadOnlyCollection<Guid> GetActiveWorldIds() => [activeWorldId];

        public Task<GameInstant> GetCurrent(
            Guid worldId,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref _currentCount);
            return Task.FromResult(GameClock.Epoch);
        }

        public Task<GameInstant> Checkpoint(
            Guid worldId,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref _checkpointCount);
            return FailCheckpoints
                ? throw new InvalidOperationException("The clock is unavailable.")
                : Task.FromResult(GameClock.Epoch);
        }

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

        public Task CheckpointActiveWorlds(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
