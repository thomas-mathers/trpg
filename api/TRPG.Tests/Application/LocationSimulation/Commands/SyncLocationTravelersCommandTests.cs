using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncLocationTravelersCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private Creature _watcherAtA = null!;
    private Creature _watcherAtB = null!;
    private Creature _traveler = null!;
    private Location _locationA = null!;
    private Location _locationB = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncLocationTravelersCommandHandler _handler = null!;
    private GetSceneQueryHandler _getScene = null!;

    // The route cycles A -> B -> A: an hour at each stop and a 2-hour leg between them, so the
    // traveler departs A at 1h, reaches B at 3h, departs B at 4h, and is back at A at 6h.
    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncLocationTravelersCommandHandler>();
        _getScene = _serviceProvider.GetRequiredService<GetSceneQueryHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId);
        _locationA = Builders.MakeLocation(WorldId, state.Id);
        _locationB = Builders.MakeLocation(WorldId, state.Id);
        _watcherAtA = Builders.MakeCreature(WorldId, locationId: _locationA.Id);
        _watcherAtB = Builders.MakeCreature(WorldId, locationId: _locationB.Id);
        _traveler = Builders.MakeCreature(
            WorldId,
            locationId: _locationA.Id,
            profession: Profession.Guard
        );
        var route = Builders.MakeCaravanRoute(WorldId);
        var connectorA = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: WorldId
        );
        var connectorB = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: WorldId
        );
        var stopA = Builders.MakeCaravanRouteStop(
            route.Id,
            0,
            _locationA.Id,
            connectorA.ConnectorId
        );
        var stopB = Builders.MakeCaravanRouteStop(
            route.Id,
            1,
            _locationB.Id,
            connectorB.ConnectorId
        );
        var routeTraveler = Builders.MakeCaravan(
            route.Id,
            WorldId,
            purpose: "Patrolling the roads."
        );

        _context.States.Add(state);
        _context.Locations.AddRange(_locationA, _locationB);
        _context.Creatures.AddRange(_watcherAtA, _watcherAtB, _traveler);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(stopA, stopB);
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.RouteTravelers.Add(routeTraveler);
        _context.RouteTravelerMembers.Add(
            Builders.MakeRouteTravelerMember(routeTraveler.Id, _traveler.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_KeepsTheTravelerAtTheLegOrigin_WhileTheyAreInTransit()
    {
        // Act
        await _handler.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = WorldId,
                LocationId = _locationA.Id,
                GameTime = At(hours: 2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var traveler = await LoadTraveler();
        Assert.Equal(_locationA.Id, traveler.LocationId);
        Assert.Equal(CreatureState.Walking, traveler.State);
    }

    [Fact]
    public async Task Handle_PlacesTheTravelerAtTheDestination_OnceTheLegCompletes()
    {
        // Act
        await _handler.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = WorldId,
                LocationId = _locationB.Id,
                GameTime = At(hours: 3.5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var traveler = await LoadTraveler();
        Assert.Equal(_locationB.Id, traveler.LocationId);
        Assert.Equal(CreatureState.Idle, traveler.State);
    }

    [Fact]
    public async Task Handle_LeavesTheTravelerWhereTheyAre_WhenRunAgainAtTheSameInstant()
    {
        // Arrange
        await _handler.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = WorldId,
                LocationId = _locationA.Id,
                GameTime = At(hours: 2),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = WorldId,
                LocationId = _locationA.Id,
                GameTime = At(hours: 2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var traveler = await LoadTraveler();
        Assert.Equal(_locationA.Id, traveler.LocationId);
        Assert.Equal(CreatureState.Walking, traveler.State);
    }

    [Fact]
    public async Task Handle_ProgressesTheTravelerThroughDepartureTransitAndArrival_AcrossRepeatedPasses()
    {
        // Act & Assert
        await SyncAt(_locationA.Id, hours: 0.5);
        var lingering = await LoadTraveler();
        Assert.Equal((_locationA.Id, CreatureState.Idle), (lingering.LocationId, lingering.State));

        await SyncAt(_locationA.Id, hours: 1.5);
        var departed = await LoadTraveler();
        Assert.Equal((_locationA.Id, CreatureState.Walking), (departed.LocationId, departed.State));

        await SyncAt(_locationA.Id, hours: 2.5);
        var traversing = await LoadTraveler();
        Assert.Equal(
            (_locationA.Id, CreatureState.Walking),
            (traversing.LocationId, traversing.State)
        );

        await SyncAt(_locationB.Id, hours: 3.5);
        var arrived = await LoadTraveler();
        Assert.Equal((_locationB.Id, CreatureState.Idle), (arrived.LocationId, arrived.State));
    }

    [Fact]
    public async Task GetScene_ShowsTheTravelerToAWatcherAtTheStop_WhileTheyLinger()
    {
        // Arrange
        await SyncAt(_locationA.Id, hours: 0.5);

        // Act
        var scene = await _getScene.Handle(
            new GetSceneQuery
            {
                WorldId = WorldId,
                PlayerId = _watcherAtA.Id,
                CurrentDate = GameClock.GetCurrentInGameDate(At(hours: 0.5)),
                GameTime = At(hours: 0.5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(scene.NearbyCreatures, creature => creature.Id == _traveler.Id);
    }

    [Fact]
    public async Task GetScene_HidesTheTravelerFromAWatcherAtTheDepartureStop_OnceTheyAreInTransit()
    {
        // Arrange
        await SyncAt(_locationA.Id, hours: 1.5);

        // Act
        var scene = await _getScene.Handle(
            new GetSceneQuery
            {
                WorldId = WorldId,
                PlayerId = _watcherAtA.Id,
                CurrentDate = GameClock.GetCurrentInGameDate(At(hours: 1.5)),
                GameTime = At(hours: 1.5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(scene.NearbyCreatures, creature => creature.Id == _traveler.Id);
    }

    [Fact]
    public async Task GetScene_HidesTheTravelerFromAWatcherAtTheDestination_WhileTheyTraverseTheRoad()
    {
        // Arrange
        await SyncAt(_locationB.Id, hours: 2.5);

        // Act
        var scene = await _getScene.Handle(
            new GetSceneQuery
            {
                WorldId = WorldId,
                PlayerId = _watcherAtB.Id,
                CurrentDate = GameClock.GetCurrentInGameDate(At(hours: 2.5)),
                GameTime = At(hours: 2.5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(scene.NearbyCreatures, creature => creature.Id == _traveler.Id);
    }

    [Fact]
    public async Task GetScene_ShowsTheTravelerToAWatcherAtTheDestination_OnceTheyArrive()
    {
        // Arrange
        await SyncAt(_locationB.Id, hours: 3.5);

        // Act
        var scene = await _getScene.Handle(
            new GetSceneQuery
            {
                WorldId = WorldId,
                PlayerId = _watcherAtB.Id,
                CurrentDate = GameClock.GetCurrentInGameDate(At(hours: 3.5)),
                GameTime = At(hours: 3.5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(scene.NearbyCreatures, creature => creature.Id == _traveler.Id);
    }

    private static GameInstant At(double hours) => GameClock.Epoch + TimeSpan.FromHours(1) * hours;

    private Task SyncAt(Guid locationId, double hours) =>
        _handler.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = WorldId,
                LocationId = locationId,
                GameTime = At(hours),
            },
            TestContext.Current.CancellationToken
        );

    private async Task<Creature> LoadTraveler()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _traveler.Id,
            TestContext.Current.CancellationToken
        );
    }
}
