using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Events;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldSimulation.Subscriptions;

public sealed class SceneResyncTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(2000, 1, 3, 13, 0, 0));
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly RecordingSink _sink = new();
    private readonly ScriptedDispatcher _dispatcher = new();
    private readonly RecordingCloser _closer = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SceneSubscriptions _subscriptions = null!;
    private WorldSimulationRunner _runner = null!;
    private Location _location = null!;
    private Guid _playerId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptions<WorldSimulationOptions>>(
                Options.Create(
                    new WorldSimulationOptions
                    {
                        ResyncAttempts = 2,
                        ResyncWindow = TimeSpan.FromMinutes(1),
                    }
                )
            )
            .WithScopedDbContexts(db.ConnectionString)
            .AddSingleton<IGameClientEventSink>(_sink)
            .AddScoped<IGameClientEventDispatcher>(_ => _dispatcher)
            .AddSingleton<IPlayerConnectionCloser>(_closer)
            .BuildServiceProvider();
        _subscriptions = _serviceProvider.GetRequiredService<SceneSubscriptions>();

        var world = Builders.MakeWorld(_worldId, Now);
        var country = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id, worldId: _worldId);
        _location = Builders.MakeLocation(_worldId, state.Id, kind: LocationKind.Wilderness);
        var player = Builders.MakeCreature(_worldId, locationId: _location.Id);
        _playerId = player.Id;
        world.PlayerId = player.Id;
        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Locations.Add(_location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var factory = _serviceProvider.GetRequiredService<WorldSimulationRunnerFactory>();
        _runner = await factory.Create(_worldId, Now, TestContext.Current.CancellationToken);
        _subscriptions.Watch(_worldId, _playerId, _location.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Tick_SendsAFreshSnapshotAndSyncs_WhenAMessageFailed()
    {
        // Arrange
        _runner.Post(new SimulationRequest(FailingWork));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        await _dispatcher.FirstFlush.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await WaitUntil(() => _subscriptions.StateOf(_worldId, _playerId) == ResyncState.Synced);
        var snapshot = Assert.Single(_sink.Events.OfType<SceneUpdatedEvent>());
        Assert.Equal(_location.Id, snapshot.Scene.LocationId);
    }

    [Fact]
    public async Task Tick_ReturnsBeforeTheResyncSendCompletes_WhenTheSendIsSlow()
    {
        // Arrange
        _dispatcher.HoldFlushes();
        _runner.Post(new SimulationRequest(FailingWork));

        // Act
        await _runner.Tick(Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ResyncState.Sent, _subscriptions.StateOf(_worldId, _playerId));
    }

    [Fact]
    public async Task Tick_ClosesTheConnectionAndDropsTheWatch_WhenResyncKeepsFailing()
    {
        // Arrange
        _dispatcher.FailFlushes();
        _runner.Post(new SimulationRequest(FailingWork));

        // Act
        await TickUntil(() => _closer.ClosedPlayerIds.Count > 0);

        // Assert
        Assert.Equal([_playerId], _closer.ClosedPlayerIds);
        Assert.Null(_subscriptions.StateOf(_worldId, _playerId));
    }

    private async Task TickUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await _runner.Tick(Now, timeout.Token);
            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static Task FailingWork(LivePlayerPoses poses, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");

    private sealed class RecordingSink : IGameClientEventSink
    {
        private readonly Lock _lock = new();
        private readonly List<GameClientEvent> _events = [];

        public IReadOnlyList<GameClientEvent> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events.ToArray();
                }
            }
        }

        public void Enqueue(GameClientEvent gameEvent)
        {
            lock (_lock)
            {
                _events.Add(gameEvent);
            }
        }
    }

    private sealed class ScriptedDispatcher : IGameClientEventDispatcher
    {
        private readonly TaskCompletionSource _firstFlush = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly TaskCompletionSource _never = new();
        private volatile bool _holdFlushes;
        private volatile bool _failFlushes;

        public Task FirstFlush => _firstFlush.Task;

        public void HoldFlushes() => _holdFlushes = true;

        public void FailFlushes() => _failFlushes = true;

        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            _firstFlush.TrySetResult();
            if (_failFlushes)
            {
                throw new InvalidOperationException("send failed");
            }

            return _holdFlushes ? _never.Task.ContinueWith(_ => true) : Task.FromResult(true);
        }
    }

    private sealed class RecordingCloser : IPlayerConnectionCloser
    {
        public List<Guid> ClosedPlayerIds { get; } = [];

        public void Close(Guid playerId) => ClosedPlayerIds.Add(playerId);
    }
}
