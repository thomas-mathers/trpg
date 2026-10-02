using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class FleeActionHandlerFlowTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private FleeActionHandler _handler = null!;
    private GameTurnSession _session = null!;
    private Guid _exitLocationId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<FleeOptions>>(
                new TestOptionsSnapshot<FleeOptions>(
                    new FleeOptions { MinimumCatchChance = 0f, MaximumCatchChance = 0f }
                )
            )
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .AddScoped<IGameClientEventAckGate, NoOpGameClientEventAckGate>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<FleeActionHandler>();

        var countryId = Guid.NewGuid();
        var state = Builders.MakeState(countryId, _worldId);
        var city = Builders.MakeCity(state.Id, countryId, worldId: _worldId);
        var currentLocation = Builders.MakeLocation(_worldId, state.Id, city.Id);
        var exitLocation = Builders.MakeLocation(_worldId, state.Id, city.Id);
        var exitConnector = Builders.MakeLocationConnector(
            currentLocation.Id,
            destinationLocationId: exitLocation.Id,
            worldId: _worldId
        );
        var player = Builders.MakeCreature(_worldId, locationId: currentLocation.Id);
        var enemy = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: currentLocation.Id
        );
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);
        _exitLocationId = exitLocation.Id;

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch + TimeSpan.FromHours(10)));
        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Locations.AddRange(currentLocation, exitLocation);
        _context.LocationConnectors.Add(exitConnector);
        _context.Creatures.AddRange(player, enemy);
        _context.GameSessions.Add(gameSession);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _serviceProvider
            .GetRequiredService<StartFightCommandHandler>()
            .Handle(
                new StartFightCommand
                {
                    SessionId = gameSession.Id,
                    WorldId = _worldId,
                    PlayerId = player.Id,
                    EnemyCreatureIds = [enemy.Id],
                    HasSurpriseRound = false,
                    PlayerWasAggressor = true,
                },
                TestContext.Current.CancellationToken
            );
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Succeeds_WhenFleeingMovesThePlayer()
    {
        // Act
        var outcome = await _handler.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task Handle_PublishesTheDestinationSceneImmediately_WhenFleeingMovesThePlayer()
    {
        // Act
        await _handler.Handle(_session, TestContext.Current.CancellationToken);

        // Assert
        var sink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var sceneEvent = sink.EnqueuedEvents.OfType<SceneUpdatedEvent>().Single();
        Assert.Equal(_exitLocationId, sceneEvent.Scene.LocationId);
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
