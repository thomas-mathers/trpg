using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncRoadTravelersCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncRoadTravelersCommandHandler _handler = null!;
    private Creature _traveler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<RoadTravelerOptions>>(
                new TestOptionsSnapshot<RoadTravelerOptions>(
                    new RoadTravelerOptions { SpeedUnitsPerHour = 5 }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncRoadTravelersCommandHandler>();

        var route = Builders.MakeCaravanRoute(WorldId);
        var routeTraveler = Builders.MakeCaravan(
            route.Id,
            WorldId,
            kind: RouteTravelerKind.Adventurer,
            purpose: "Seeking work in the next city."
        );
        _traveler = Builders.MakeCreature(WorldId, locationId: LocationB);
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
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            Builders.MakeCaravanRouteStop(route.Id, 0, LocationA, connectorA.ConnectorId),
            Builders.MakeCaravanRouteStop(route.Id, 1, LocationB, connectorB.ConnectorId)
        );
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.RouteTravelers.Add(routeTraveler);
        _context.Creatures.Add(_traveler);
        _context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = WorldId,
                RouteTravelerId = routeTraveler.Id,
                CreatureId = _traveler.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RelocatesTravelerAndSetsTravelingState_WhileInTransit()
    {
        await _handler.Handle(
            new SyncRoadTravelersCommand
            {
                WorldId = WorldId,
                LocationId = LocationA,
                Playtime = GameClock.RealTimePerInGameHour * 2,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var traveler = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _traveler.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(LocationA, traveler.LocationId);
        Assert.Equal(CreatureState.Traveling, traveler.State);
    }

    [Fact]
    public async Task Handle_DoesNotRelocateADeadTraveler()
    {
        _traveler.State = CreatureState.Dead;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new SyncRoadTravelersCommand
            {
                WorldId = WorldId,
                LocationId = LocationA,
                Playtime = GameClock.RealTimePerInGameHour * 2,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var traveler = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == _traveler.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(LocationB, traveler.LocationId);
        Assert.Equal(CreatureState.Dead, traveler.State);
    }
}
