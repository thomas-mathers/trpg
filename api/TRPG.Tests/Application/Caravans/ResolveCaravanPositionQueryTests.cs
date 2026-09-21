using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Caravans;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Caravans;

public sealed class ResolveCaravanPositionQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveCaravanPositionQueryHandler _handler = null!;
    private Caravan _caravan = null!;

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
        _handler = _serviceProvider.GetRequiredService<ResolveCaravanPositionQueryHandler>();

        var route = Builders.MakeCaravanRoute(WorldId, lingerHours: 1);
        var stopA = Builders.MakeCaravanRouteStop(route.Id, 0, LocationA, distanceToNextStop: 10);
        var stopB = Builders.MakeCaravanRouteStop(route.Id, 1, LocationB, distanceToNextStop: 10);
        _caravan = Builders.MakeCaravan(route.Id, WorldId);

        _context.CaravanRoutes.Add(route);
        _context.CaravanRouteStops.AddRange(stopA, stopB);
        _context.Caravans.Add(_caravan);
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
            new ResolveCaravanPositionQuery { CaravanId = _caravan.Id, Playtime = TimeSpan.Zero },
            TestContext.Current.CancellationToken
        );

        // Assert
        var lingering = Assert.IsType<CaravanPosition.Lingering>(position);
        Assert.Equal(LocationA, lingering.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsInTransit_WhilePastTheLingerWindow()
    {
        // Act
        var position = await _handler.Handle(
            new ResolveCaravanPositionQuery
            {
                CaravanId = _caravan.Id,
                Playtime = GameClock.RealTimePerInGameHour * 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<CaravanPosition.InTransit>(position);
        Assert.Equal(LocationA, inTransit.FromLocationId);
        Assert.Equal(LocationB, inTransit.ToLocationId);
    }

    [Fact]
    public async Task Handle_WalksStopsBackward_ForACounterClockwiseCaravan()
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
        var counterClockwiseCaravan = Builders.MakeCaravan(
            route.Id,
            WorldId,
            direction: CaravanDirection.CounterClockwise
        );
        _context.CaravanRoutes.Add(route);
        _context.CaravanRouteStops.AddRange(stopX, stopY, stopZ);
        _context.Caravans.Add(counterClockwiseCaravan);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var position = await _handler.Handle(
            new ResolveCaravanPositionQuery
            {
                CaravanId = counterClockwiseCaravan.Id,
                Playtime = GameClock.RealTimePerInGameHour * 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var inTransit = Assert.IsType<CaravanPosition.InTransit>(position);
        Assert.Equal(locationX, inTransit.FromLocationId);
        Assert.Equal(locationZ, inTransit.ToLocationId);
    }
}
