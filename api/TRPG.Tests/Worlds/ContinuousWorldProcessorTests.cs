using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Events;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using TRPG.Worlds;

namespace TRPG.Tests.Worlds;

public sealed class ContinuousWorldProcessorTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );
    private readonly WorldMutationGate _gate = new();
    private readonly RecordingLogger _logger = new();
    private readonly List<GameClientEvent> _flushedEvents = [];

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WorldClock _worldClock = null!;
    private ContinuousWorldProcessor _processor = null!;
    private World _world = null!;
    private Creature _sleeper = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithScopedDbContexts(db.ConnectionString)
            .AddSingleton(_flushedEvents)
            .AddScoped<IGameClientEventDispatcher, RecordingGameClientEventDispatcher>()
            .BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _worldClock = new WorldClock(scopeFactory, _timeProvider);
        _processor = new ContinuousWorldProcessor(scopeFactory, _worldClock, _gate, _logger);

        _world = Builders.MakeWorld();
        var country = Builders.MakeCountry(_world.Id);
        var state = Builders.MakeState(country.Id);
        var location = Builders.MakeLocation(_world.Id, state.Id, kind: LocationKind.Wilderness);
        _player = Builders.MakeCreature(_world.Id, locationId: location.Id, currentHp: 1);
        _sleeper = Builders.MakeCreature(_world.Id, locationId: location.Id);
        _world.PlayerId = _player.Id;
        _context.Worlds.Add(_world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.Add(location);
        _context.Creatures.AddRange(_player, _sleeper);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                _sleeper.Id,
                action: CreatureJobAction.Sleep,
                startHour: 6,
                endHour: 22,
                locationId: location.Id,
                worldId: _world.Id,
                priority: 100
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ProcessRoutines_AppliesJobRoutine_WhenWorldIsActive()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CreatureState.Sleeping, await ReadSleeperState());
    }

    [Fact]
    public async Task ProcessRoutines_LeavesWorldUntouched_WhenWorldIsPaused()
    {
        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_sleeper.State, await ReadSleeperState());
    }

    [Fact]
    public async Task ProcessFrequent_CheckpointsClock_WhenWorldIsActive()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        // Act
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(GameClock.Epoch + TimeSpan.FromMinutes(10), await ReadPersistedGameTime());
    }

    [Fact]
    public async Task ProcessFrequent_RegeneratesPlayerAndFlushesVitals_WhenWorldIsActive()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Act
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        var vitalsChanged = Assert.Single(_flushedEvents.OfType<PlayerVitalsChangedEvent>());
        Assert.Equal(_player.Id, vitalsChanged.Vitals.CreatureId);
        Assert.Equal(await ReadPlayerHp(), vitalsChanged.Vitals.CurrentHp);
        Assert.True(vitalsChanged.Vitals.CurrentHp > 1);
    }

    [Fact]
    public async Task ProcessFrequent_LeavesPlayerUntouched_WhenWorldIsPaused()
    {
        // Act
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, await ReadPlayerHp());
        Assert.Empty(_flushedEvents);
    }

    [Fact]
    public async Task ProcessRoutines_DoesNotRegenerate_WhenWorldIsActive()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, await ReadPlayerHp());
        Assert.Empty(_flushedEvents.OfType<PlayerVitalsChangedEvent>());
    }

    [Fact]
    public async Task ProcessFrequent_DoesNotRegeneratePlayer_WhenPlayerIsInAnActiveFight()
    {
        // Arrange
        _context.Encounters.Add(Builders.MakeFight(_world.Id, _player.Id, [_player.Id]));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await _processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, await ReadPlayerHp());
        Assert.Empty(_flushedEvents.OfType<PlayerVitalsChangedEvent>());
    }

    [Fact]
    public async Task ProcessRoutines_PublishesVersionedScene_WhenNoSceneWasPublishedYet()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(_logger.Errors.Count == 0, string.Join(Environment.NewLine, _logger.Errors));
        var sceneUpdated = Assert.Single(_flushedEvents.OfType<SceneUpdatedEvent>());
        Assert.Equal(_player.Id, sceneUpdated.Scene.Player.Id);
        Assert.Equal(1, sceneUpdated.Stamp.Version);
    }

    [Fact]
    public async Task ProcessRoutines_PublishesNothing_WhenSceneHasNoPlayerVisibleChange()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);
        _flushedEvents.Clear();
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_flushedEvents.OfType<SceneUpdatedEvent>());
    }

    [Fact]
    public async Task ProcessRoutines_PublishesScene_WhenACreatureArrivesAtTheLocation()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);
        _flushedEvents.Clear();
        _context.Creatures.Add(Builders.MakeCreature(_world.Id, locationId: _player.LocationId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        var sceneUpdated = Assert.Single(_flushedEvents.OfType<SceneUpdatedEvent>());
        Assert.Equal(2, sceneUpdated.Stamp.Version);
        Assert.Equal(2, sceneUpdated.Scene.NearbyCreatures.Count);
    }

    [Fact]
    public async Task ProcessRoutines_WaitsForMutationLease_WhenGameplayOperationIsRunning()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        var lease = await _gate.Acquire(_world.Id, TestContext.Current.CancellationToken);

        // Act
        var pass = _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(pass.IsCompleted);
        Assert.Equal(_sleeper.State, await ReadSleeperState());
        await lease.DisposeAsync();
        await pass;
        Assert.Equal(CreatureState.Sleeping, await ReadSleeperState());
    }

    [Fact]
    public async Task ProcessRoutines_SkipsPass_WhenPreviousPassForWorldIsStillRunning()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        var lease = await _gate.Acquire(_world.Id, TestContext.Current.CancellationToken);
        var stalePass = _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Act
        var overlappingPass = _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        await overlappingPass.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        Assert.False(stalePass.IsCompleted);
        await lease.DisposeAsync();
        await stalePass;
    }

    [Fact]
    public async Task ProcessRoutines_RunsNextPass_AfterPreviousPassFinishes()
    {
        // Arrange
        await _worldClock.ResumeWorld(_world.Id, TestContext.Current.CancellationToken);
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);
        await _context
            .Creatures.Where(creature => creature.Id == _sleeper.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.State, CreatureState.Idle),
                TestContext.Current.CancellationToken
            );

        // Act
        await _processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CreatureState.Sleeping, await ReadSleeperState());
    }

    [Fact]
    public async Task ProcessRoutines_LogsFailureWithoutThrowing_WhenWorldPassFails()
    {
        // Arrange
        var processor = new ContinuousWorldProcessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new FailingWorldClock(_world.Id),
            _gate,
            _logger
        );

        // Act
        await processor.ProcessRoutines(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(_logger.Errors, error => error.Contains(_world.Id.ToString()));
    }

    [Fact]
    public async Task ProcessFrequent_LogsFailureWithoutThrowing_WhenClockCheckpointFails()
    {
        // Arrange
        var processor = new ContinuousWorldProcessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new FailingWorldClock(_world.Id),
            _gate,
            _logger
        );

        // Act
        await processor.ProcessFrequent(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(_logger.Errors);
    }

    private async Task<CreatureState> ReadSleeperState()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.Where(creature => creature.Id == _sleeper.Id)
            .Select(creature => creature.State)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ReadPlayerHp()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.Where(creature => creature.Id == _player.Id)
            .Select(creature => creature.CurrentHp)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<GameInstant> ReadPersistedGameTime()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Worlds.Where(world => world.Id == _world.Id)
            .Select(world => world.GameTime)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed class RecordingGameClientEventDispatcher(
        TestGameClientEventSink sink,
        List<GameClientEvent> flushedEvents
    ) : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            flushedEvents.AddRange(sink.EnqueuedEvents);
            var flushedAny = sink.EnqueuedEvents.Count > 0;
            sink.EnqueuedEvents.Clear();
            return Task.FromResult(flushedAny);
        }
    }

    private sealed class RecordingLogger : ILogger<ContinuousWorldProcessor>
    {
        private readonly List<string> _errors = [];

        public IReadOnlyList<string> Errors
        {
            get
            {
                lock (_errors)
                {
                    return _errors.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel < LogLevel.Error)
            {
                return;
            }

            lock (_errors)
            {
                _errors.Add($"{formatter(state, exception)} {exception}");
            }
        }
    }

    private sealed class FailingWorldClock(Guid activeWorldId) : IWorldClock
    {
        public IReadOnlyCollection<Guid> GetActiveWorldIds() => [activeWorldId];

        public Task<GameInstant> GetCurrent(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("The clock is unavailable.");

        public Task<GameInstant> Checkpoint(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("The clock is unavailable.");

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
