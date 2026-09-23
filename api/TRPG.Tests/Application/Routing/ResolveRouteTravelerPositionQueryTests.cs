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
    private const float SpeedUnitsPerHour = 5;

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

        var route = Builders.MakeCaravanRoute(WorldId, lingerHours: 1);
        var stopA = Builders.MakeCaravanRouteStop(route.Id, 0, LocationA, distanceToNextStop: 10);
        var stopB = Builders.MakeCaravanRouteStop(route.Id, 1, LocationB, distanceToNextStop: 10);
        _traveler = Builders.MakeCaravan(route.Id, WorldId);

        _context.Routes.Add(route);
        _context.RouteStops.AddRange(stopA, stopB);
        _context.RouteTravelers.Add(_traveler);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsLingeringAtTheFirstStop_AtPlaytimeZero()
    {
        // Act
        var position = await _handler.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = _traveler.Id,
                Playtime = TimeSpan.Zero,
                SpeedUnitsPerHour = SpeedUnitsPerHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var lingering = Assert.IsType<RoutePosition.Lingering>(position);
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
                Playtime = GameClock.RealTimePerInGameHour * 2,
                SpeedUnitsPerHour = SpeedUnitsPerHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<RoutePosition.InTransit>(position);
        Assert.Equal(LocationA, inTransit.FromLocationId);
        Assert.Equal(LocationB, inTransit.ToLocationId);
    }

    [Fact]
    public async Task Handle_WalksStopsBackward_ForACounterClockwiseTraveler()
    {
        // Arrange — a separate 3-stop route so the counter-clockwise leg order (X -> Z -> Y) is
        // distinguishable from clockwise (X -> Y -> Z).
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var locationZ = Guid.NewGuid();
        var route = Builders.MakeCaravanRoute(WorldId, lingerHours: 1);
        var stopX = Builders.MakeCaravanRouteStop(route.Id, 0, locationX, distanceToNextStop: 10);
        var stopY = Builders.MakeCaravanRouteStop(route.Id, 1, locationY, distanceToNextStop: 20);
        var stopZ = Builders.MakeCaravanRouteStop(route.Id, 2, locationZ, distanceToNextStop: 5);
        var counterClockwiseTraveler = Builders.MakeCaravan(
            route.Id,
            WorldId,
            direction: RouteDirection.CounterClockwise
        );
        _context.Routes.Add(route);
        _context.RouteStops.AddRange(stopX, stopY, stopZ);
        _context.RouteTravelers.Add(counterClockwiseTraveler);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var position = await _handler.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = counterClockwiseTraveler.Id,
                Playtime = GameClock.RealTimePerInGameHour * 1,
                SpeedUnitsPerHour = SpeedUnitsPerHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<RoutePosition.InTransit>(position);
        Assert.Equal(locationX, inTransit.FromLocationId);
        Assert.Equal(locationZ, inTransit.ToLocationId);
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
                Playtime = TimeSpan.Zero,
                SpeedUnitsPerHour = SpeedUnitsPerHour,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, positions.Count);
        Assert.IsType<RoutePosition.Lingering>(positions[_traveler.Id].Position);
        Assert.Equal(LocationB, positions[_traveler.Id].NextLocationId);
        Assert.IsType<RoutePosition.Lingering>(positions[secondTraveler.Id].Position);
        Assert.Equal(LocationA, positions[secondTraveler.Id].NextLocationId);
    }
}
