using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Application.Scenes.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns;

public sealed class BoardCaravanActionHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _originLocationId = Guid.NewGuid();
    private readonly Guid _destinationLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private BoardCaravanActionHandler _handler = null!;
    private GameTurnSession _session = null!;
    private Guid _caravanId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CaravanOptions>>(
                new TestOptionsSnapshot<CaravanOptions>(
                    new CaravanOptions { SpeedUnitsPerHour = 5 }
                )
            )
            .AddScoped<IGameClientEventDispatcher, NoOpGameClientEventDispatcher>()
            .AddScoped<IGameClientEventAckGate, NoOpGameClientEventAckGate>()
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<BoardCaravanActionHandler>();

        var sanctuary = await SanctuarySeeder.SeedCityWithTemple(
            _context,
            _worldId,
            _originLocationId
        );
        var destinationLocation = Builders.MakeLocation(
            _worldId,
            sanctuary.StateId,
            id: _destinationLocationId
        );
        var route = Builders.MakeCaravanRoute(_worldId);
        var stopAB = Builders.MakeLocationConnector(
            _originLocationId,
            _destinationLocationId,
            worldId: _worldId
        );
        var stopBA = Builders.MakeLocationConnector(
            _destinationLocationId,
            _originLocationId,
            worldId: _worldId
        );
        var travelConnectorAB = Builders.MakeTravelConnector(
            stopAB.Id,
            worldId: _worldId,
            distance: 10
        );
        var travelConnectorBA = Builders.MakeTravelConnector(
            stopBA.Id,
            worldId: _worldId,
            distance: 10
        );
        var originStop = Builders.MakeCaravanRouteStop(route.Id, 0, _originLocationId, stopAB.Id);
        var destinationStop = Builders.MakeCaravanRouteStop(
            route.Id,
            1,
            _destinationLocationId,
            stopBA.Id
        );
        var caravan = Builders.MakeCaravan(route.Id, _worldId);
        var player = Builders.MakeCreature(_worldId, locationId: _originLocationId);
        var gameSession = Builders.MakeGameSession(_worldId, player.Id);
        var ticket = Builders.MakeCaravanTicket(
            caravan.Id,
            player.Id,
            _originLocationId,
            _destinationLocationId
        );
        _session = new GameTurnSession(gameSession.Id, _worldId, player.Id);
        _caravanId = caravan.Id;

        _context.Worlds.Add(Builders.MakeWorld(_worldId, GameClock.Epoch));
        _context.Locations.Add(destinationLocation);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(originStop, destinationStop);
        _context.RouteTravelers.Add(caravan);
        _context.LocationConnectors.AddRange(stopAB, stopBA);
        _context.TravelConnectors.AddRange(travelConnectorAB, travelConnectorBA);
        _context.Creatures.Add(player);
        _context.GameSessions.Add(gameSession);
        _context.CaravanTickets.Add(ticket);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_Succeeds_WhenThePlayerBoardsTheCaravan()
    {
        // Act
        var outcome = await _handler.Handle(
            _session,
            _caravanId,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task Handle_PublishesTheDestinationScene_WhenThePlayerBoardsTheCaravan()
    {
        // Act
        await _handler.Handle(_session, _caravanId, TestContext.Current.CancellationToken);

        // Assert
        var sink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var sceneEvent = sink.EnqueuedEvents.OfType<SceneUpdatedEvent>().Single();
        Assert.Equal(_destinationLocationId, sceneEvent.Scene.LocationId);
    }

    [Fact]
    public async Task Handle_RefusesToBoard_WhenThePlayerHasALingeringEffect()
    {
        // Arrange
        var player = await _context.Creatures.SingleAsync(
            creature => creature.Id == _session.PlayerId,
            TestContext.Current.CancellationToken
        );
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
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var outcome = await _handler.Handle(
            _session,
            _caravanId,
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var unmoved = await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _session.PlayerId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(ActionFailure.Afflicted, outcome.Failure);
        Assert.Equal(_originLocationId, unmoved.LocationId);
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
