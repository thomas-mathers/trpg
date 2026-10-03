using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class WaitActionHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WaitActionHandler _handler = null!;
    private GameTurnSession _session = null!;
    private TestGameClientEventSink _events = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<WaitActionHandler>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: _worldId);
        var location = Builders.MakeLocation(_worldId, state.Id);
        var player = Builders.MakeCreature(_worldId, locationId: location.Id);
        player.Posture = CreaturePosture.Sitting;
        player.ActiveDots =
        [
            new ActiveDot
            {
                AbilityName = "Ignite",
                Amount = 1,
                DamageType = nameof(DamageType.Fire),
                NextTickAt = GameClock.Epoch + TimeSpan.FromSeconds(6),
                ExpiresAt = GameClock.Epoch + TimeSpan.FromDays(1),
            },
        ];
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch));
        _context.States.Add(state);
        _context.Locations.Add(location);
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
    public async Task Handle_RefusesToWait_WhenThePlayerHasALingeringEffect()
    {
        // Act
        var outcome = await _handler.Handle(_session, 1, 0, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var world = await verifyContext.Worlds.SingleAsync(
            world => world.Id == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ActionFailure.Afflicted, outcome.Failure);
        Assert.Equal(GameClock.Epoch, world.GameTime);
    }

    [Fact]
    public async Task Handle_PublishesClockReanchor_WhenTimeAdvancesAtTheSameLocation()
    {
        var player = await _context.Creatures.SingleAsync(
            creature => creature.Id == _session.PlayerId,
            TestContext.Current.CancellationToken
        );
        player.ActiveDots = [];
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var publishScene = _serviceProvider.GetRequiredService<PublishAmbientSceneCommandHandler>();
        await publishScene.Handle(
            new PublishAmbientSceneCommand
            {
                WorldId = _worldId,
                PlayerId = player.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );
        _events.EnqueuedEvents.Clear();

        var outcome = await _handler.Handle(_session, 0, 1, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Contains(_events.EnqueuedEvents, gameEvent => gameEvent is ClockReanchoredEvent);
    }

    private sealed class NoOpGameClientEventDispatcher : IGameClientEventDispatcher
    {
        public Task<bool> FlushAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
