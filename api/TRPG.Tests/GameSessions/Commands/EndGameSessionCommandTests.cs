using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Narration.Queries;
using TRPG.Application.WorldSimulation;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.GameSessions.Commands;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.GameSessions.Commands;

public sealed class EndGameSessionCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly World _world = Builders.MakeWorld();
    private readonly Creature _player = Builders.MakeCreature();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EndGameSessionCommandHandler _handler = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .WithSimulationPump(db.ConnectionString)
            .BuildServiceProvider();
        _serviceProvider.GetRequiredService<SimulationPump>();
        _handler = _serviceProvider.GetRequiredService<EndGameSessionCommandHandler>();

        _session = Builders.MakeGameSession(_world.Id, _player.Id);
        _context.Worlds.Add(_world);
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ClearsTheLoreAnchorCaches_ForTheSessionsWorld()
    {
        // Arrange
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var loreAnchorsKey = GetLoreAnchorsByWorldQueryHandler.CacheKey(_world.Id);
        var automatonKey = GetLoreAnchorAutomatonByWorldQueryHandler.CacheKey(_world.Id);
        cache.Set(loreAnchorsKey, Array.Empty<object>());
        cache.Set(automatonKey, new object());

        // Act
        await _handler.Handle(
            new EndGameSessionCommand { SessionId = _session.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(cache.TryGetValue(loreAnchorsKey, out _));
        Assert.False(cache.TryGetValue(automatonKey, out _));
    }

    [Fact]
    public async Task Handle_WaitsForTheRunnersCurrentRequest_WhenOneIsInFlight()
    {
        // Arrange
        var coordinator = _serviceProvider.GetRequiredService<WorldSimulationCoordinator>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = coordinator.Request(
            _world.Id,
            async (_, _) =>
            {
                started.SetResult();
                await release.Task;
            },
            TestContext.Current.CancellationToken
        );
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Act
        var ending = _handler.Handle(
            new EndGameSessionCommand { SessionId = _session.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(ending.IsCompleted);
        release.SetResult();
        await inFlight;
        await ending;
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.GameSessions.AnyAsync(
                session => session.Id == _session.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_PreservesActiveFightAndItsEngagement_WhenSessionEnds()
    {
        // Arrange
        var fight = Builders.MakeFight(_world.Id, _player.Id, [_player.Id]);
        _player.IsEngaged = true;
        _context.Encounters.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new EndGameSessionCommand { SessionId = _session.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedFight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.Id == fight.Id, TestContext.Current.CancellationToken);
        var player = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(updatedFight.CompletedAt);
        Assert.Equal(CombatOutcome.Ongoing, updatedFight.Outcome);
        Assert.True(player.IsEngaged);
    }
}
