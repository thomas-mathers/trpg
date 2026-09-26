using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Caravans;

public sealed class GetNextCaravanArrivalsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    // Each test's route shares a database with every other test in this class (no rollback
    // between tests), and the query scopes by WorldId + LocationId — instance fields (fresh per
    // test) keep one test's caravans from leaking into another's results.
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNextCaravanArrivalsQueryHandler _handler = null!;
    private Route _route = null!;

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
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNextCaravanArrivalsQueryHandler>();

        // 2 stops, 10 units apart each way at speed 5 = 2 leg hours; with a 1-hour linger the
        // total cycle is 2 * (1 + 2) = 6 hours, and stop A's own linger window is [0, 1).
        _route = Builders.MakeCaravanRoute(_worldId);
        var connectorA = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        var connectorB = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        var stopA = Builders.MakeCaravanRouteStop(_route.Id, 0, _locationA, connectorA.ConnectorId);
        var stopB = Builders.MakeCaravanRouteStop(_route.Id, 1, _locationB, connectorB.ConnectorId);
        var fare = Builders.MakeCaravanFare(_route.Id, _worldId);
        _context.Routes.Add(_route);
        _context.RouteSteps.AddRange(stopA, stopB);
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.CaravanFares.Add(fare);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenACaravanIsAlreadyLingeringAtTheStop()
    {
        // Arrange
        _context.RouteTravelers.Add(Builders.MakeCaravan(_route.Id, _worldId, phaseOffsetHours: 0));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var arrivals = await _handler.Handle(
            new GetNextCaravanArrivalsQuery
            {
                WorldId = _worldId,
                LocationId = _locationA,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var arrival = Assert.Single(arrivals);
        Assert.Equal(_route.Name, arrival.RouteName);
        Assert.Equal(0, arrival.HoursUntilArrival);
    }

    [Fact]
    public async Task Handle_ReturnsHoursUntilArrival_OncePastTheLingerWindow()
    {
        // Arrange
        _context.RouteTravelers.Add(Builders.MakeCaravan(_route.Id, _worldId, phaseOffsetHours: 0));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var arrivals = await _handler.Handle(
            new GetNextCaravanArrivalsQuery
            {
                WorldId = _worldId,
                LocationId = _locationA,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — stop A's window [0, 1) closed 1 hour ago; the next occurrence is a full 6-hour
        // cycle later, minus the 2 hours already elapsed.
        var arrival = Assert.Single(arrivals);
        Assert.Equal(4, arrival.HoursUntilArrival);
    }

    [Fact]
    public async Task Handle_ReturnsTheSoonestInstance_WhenSeveralShareARoute()
    {
        // Arrange
        _context.RouteTravelers.AddRange(
            Builders.MakeCaravan(_route.Id, _worldId, phaseOffsetHours: 0),
            Builders.MakeCaravan(_route.Id, _worldId, phaseOffsetHours: 3)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var arrivals = await _handler.Handle(
            new GetNextCaravanArrivalsQuery
            {
                WorldId = _worldId,
                LocationId = _locationA,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — one instance is lingering right now (0 hours); the other is still 3 hours out.
        var arrival = Assert.Single(arrivals);
        Assert.Equal(0, arrival.HoursUntilArrival);
    }

    [Fact]
    public async Task Handle_ReturnsOneEntryPerRoute_WhenBothServeTheStop()
    {
        // Arrange
        var secondRoute = Builders.MakeCaravanRoute(
            _worldId,
            name: "The Capital Circuit — Counter-clockwise"
        );
        var connectorA = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        var connectorB = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        _context.Routes.Add(secondRoute);
        _context.RouteSteps.AddRange(
            Builders.MakeCaravanRouteStop(secondRoute.Id, 0, _locationA, connectorA.ConnectorId),
            Builders.MakeCaravanRouteStop(secondRoute.Id, 1, _locationB, connectorB.ConnectorId)
        );
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.CaravanFares.Add(Builders.MakeCaravanFare(secondRoute.Id, _worldId));
        _context.RouteTravelers.AddRange(
            Builders.MakeCaravan(_route.Id, _worldId),
            Builders.MakeCaravan(secondRoute.Id, _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var arrivals = await _handler.Handle(
            new GetNextCaravanArrivalsQuery
            {
                WorldId = _worldId,
                LocationId = _locationA,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            [_route.Name, secondRoute.Name],
            arrivals.Select(arrival => arrival.RouteName).OrderBy(name => name)
        );
    }
}
