using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Routing.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing;

public sealed class ResolveRouteTravelerPositionQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveRouteTravelerPositionQueryHandler _handler = null!;
    private RouteTraveler _traveler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveRouteTravelerPositionQueryHandler>();

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
        var stopA = Builders.MakeCaravanRouteStop(route.Id, 0, LocationA, connectorA.ConnectorId);
        var stopB = Builders.MakeCaravanRouteStop(route.Id, 1, LocationB, connectorB.ConnectorId);
        _traveler = Builders.MakeCaravan(route.Id, WorldId);

        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(stopA, stopB);
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.RouteTravelers.Add(_traveler);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsLingeringAtTheFirstStop_AtGameTimeZero()
    {
        // Act
        var position = await _handler.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = _traveler.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var lingering = Assert.IsType<RouteTimelinePosition.Lingering>(position);
        Assert.Equal(LocationA, lingering.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsInTransit_WhilePastTheLingerWindow()
    {
        // Act
        var position = await _handler.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = _traveler.Id,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<RouteTimelinePosition.InTransit>(position);
        Assert.Equal(LocationA, inTransit.FromLocationId);
        Assert.Equal(LocationB, inTransit.ToLocationId);
    }

    [Fact]
    public async Task Handle_FollowsTheStoredStepOrder()
    {
        // Arrange — a separate 3-stop route so the counter-clockwise leg order (X -> Z -> Y) is
        // distinguishable from clockwise (X -> Y -> Z).
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var locationZ = Guid.NewGuid();
        var route = Builders.MakeCaravanRoute(WorldId);
        var connectorX = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: WorldId
        );
        var connectorY = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 20,
            worldId: WorldId
        );
        var connectorZ = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 5,
            worldId: WorldId
        );
        var stopX = Builders.MakeCaravanRouteStop(route.Id, 0, locationX, connectorX.ConnectorId);
        var stopY = Builders.MakeCaravanRouteStop(route.Id, 1, locationY, connectorY.ConnectorId);
        var stopZ = Builders.MakeCaravanRouteStop(route.Id, 2, locationZ, connectorZ.ConnectorId);
        var traveler = Builders.MakeCaravan(route.Id, WorldId);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(stopX, stopY, stopZ);
        _context.TravelConnectors.AddRange(connectorX, connectorY, connectorZ);
        _context.RouteTravelers.Add(traveler);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var position = await _handler.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = traveler.Id,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<RouteTimelinePosition.InTransit>(position);
        Assert.Equal(locationX, inTransit.FromLocationId);
        Assert.Equal(locationY, inTransit.ToLocationId);
    }

    [Fact]
    public async Task Handle_ResolvesMultipleTravelers_InOneBatch()
    {
        var secondTraveler = Builders.MakeCaravan(_traveler.RouteId, WorldId, phaseOffsetHours: 3);
        _context.RouteTravelers.Add(secondTraveler);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler =
            _serviceProvider.GetRequiredService<ResolveRouteTravelerPositionsQueryHandler>();

        var positions = await handler.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = [_traveler.Id, secondTraveler.Id],
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, positions.Count);
        Assert.IsType<RouteTimelinePosition.Lingering>(positions[_traveler.Id].Position);
        Assert.Equal(LocationB, positions[_traveler.Id].NextLocationId);
        Assert.IsType<RouteTimelinePosition.Lingering>(positions[secondTraveler.Id].Position);
        Assert.Equal(LocationA, positions[secondTraveler.Id].NextLocationId);
    }
}
