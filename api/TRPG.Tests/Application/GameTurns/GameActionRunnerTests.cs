using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldSimulation;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class GameActionRunnerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly List<string> _log = [];
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GameActionRunner _runner = null!;
    private GameTurnSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithSimulationPump(db.ConnectionString)
            .AddScoped<IGameClientEventDispatcher>(_ => new LoggingDispatcher(_log))
            .BuildServiceProvider();
        _runner = _serviceProvider.GetRequiredService<GameActionRunner>();
        _serviceProvider.GetRequiredService<SimulationPump>();

        var player = Builders.MakeCreature(_worldId);
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);
        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch));
        _context.Creatures.Add(player);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Run_FlushesEvents_AfterTheActionCompleted()
    {
        // Act
        await _runner.Run(
            _session,
            _ =>
            {
                _log.Add("action");
                return Task.FromResult(ActionOutcome.Failed(ActionFailure.PlayerNotIdle));
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(["action", "flush"], _log);
    }

    [Fact]
    public async Task Run_RunsSimultaneousActionsOnOneWorldOneAfterTheOther()
    {
        // Arrange
        var running = 0;
        var mostRunning = 0;

        async Task<ActionOutcome> Act(CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMaximum(ref mostRunning, now);
            await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
            Interlocked.Decrement(ref running);
            return ActionOutcome.Failed(ActionFailure.PlayerNotIdle);
        }

        // Act
        await Task.WhenAll(
            _runner.Run(_session, Act, TestContext.Current.CancellationToken),
            _runner.Run(_session, Act, TestContext.Current.CancellationToken),
            _runner.Run(_session, Act, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(1, mostRunning);
    }

    [Fact]
    public async Task Run_HandsTheActionTheLivePosesOfTheWorld()
    {
        // Arrange
        var coordinator = _serviceProvider.GetRequiredService<WorldSimulationCoordinator>();
        var requestedPoses = await coordinator.Request(
            _worldId,
            (poses, _) => Task.FromResult(poses),
            TestContext.Current.CancellationToken
        );

        // Act
        var seenPoses = default(object);
        await _runner.Run(
            _session,
            (poses, _) =>
            {
                seenPoses = poses;
                return Task.FromResult(ActionOutcome.Failed(ActionFailure.PlayerNotIdle));
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(requestedPoses, seenPoses);
    }

    [Fact]
    public async Task Run_Throws_WhenTheWorldSimulationIsUnavailable()
    {
        // Arrange
        var coordinator = _serviceProvider.GetRequiredService<WorldSimulationCoordinator>();
        coordinator.MarkUnavailable(_worldId);

        // Act
        var act = () =>
            _runner.Run(
                _session,
                _ => Task.FromResult(ActionOutcome.Success),
                TestContext.Current.CancellationToken
            );

        // Assert
        await Assert.ThrowsAsync<WorldUnavailableException>(act);
    }

    private static void InterlockedMaximum(ref int location, int value)
    {
        int current;
        do
        {
            current = location;
        } while (
            value > current && Interlocked.CompareExchange(ref location, value, current) != current
        );
    }

    private sealed class LoggingDispatcher(List<string> log) : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            log.Add("flush");
            return Task.FromResult(false);
        }
    }
}
