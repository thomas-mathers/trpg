using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class RespawnActionHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RespawnActionHandler _handler = null!;
    private GameTurnSession _session = null!;
    private Guid _sanctuaryLocationId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .AddScoped<IGameClientEventAckGate, NoOpGameClientEventAckGate>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RespawnActionHandler>();

        var player = Builders.MakeCreature(
            _worldId,
            condition: CreatureCondition.Dead,
            currentHp: 0
        );
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.Creatures.Add(player);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sanctuary = await SanctuarySeeder.SeedCityWithTemple(
            _context,
            _worldId,
            player.LocationId
        );
        _sanctuaryLocationId = sanctuary.SanctuaryLocationId;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Succeeds_WhenThePlayerRespawns()
    {
        // Act
        var outcome = await _handler.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task Handle_PublishesTheSanctuarySceneImmediately_WhenThePlayerRespawns()
    {
        // Act
        await _handler.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        var sink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var sceneEvent = sink.EnqueuedEvents.OfType<SceneUpdatedEvent>().Single();
        Assert.Equal(_sanctuaryLocationId, sceneEvent.Scene.LocationId);
    }

    private sealed class NoOpGameClientEventDispatcher : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class NoOpGameClientEventAckGate : IGameClientEventAckGate
    {
        public Task FlushAndAwaitAckAsync(
            Guid worldId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
