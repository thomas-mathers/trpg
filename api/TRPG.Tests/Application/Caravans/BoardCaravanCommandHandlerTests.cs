using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Routing.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Caravans;

public sealed class BoardCaravanCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private BoardCaravanCommandHandler _handler = null!;
    private RouteTraveler _caravan = null!;
    private Creature _player = null!;

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
        _handler = _serviceProvider.GetRequiredService<BoardCaravanCommandHandler>();

        var route = Builders.MakeCaravanRoute(WorldId);

        var stopAB = Builders.MakeLocationConnector(LocationA, LocationB, worldId: WorldId);
        var stopBA = Builders.MakeLocationConnector(LocationB, LocationA, worldId: WorldId);
        var travelConnectorAB = Builders.MakeTravelConnector(
            stopAB.Id,
            worldId: WorldId,
            distance: 10
        );
        var travelConnectorBA = Builders.MakeTravelConnector(
            stopBA.Id,
            worldId: WorldId,
            distance: 10
        );

        var stopA = Builders.MakeCaravanRouteStop(route.Id, 0, LocationA, stopAB.Id);
        var stopB = Builders.MakeCaravanRouteStop(route.Id, 1, LocationB, stopBA.Id);
        _caravan = Builders.MakeCaravan(route.Id, WorldId);
        _player = Builders.MakeCreature(worldId: WorldId, locationId: LocationA);

        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(stopA, stopB);
        _context.RouteTravelers.Add(_caravan);
        _context.Creatures.Add(_player);
        _context.LocationConnectors.AddRange(stopAB, stopBA);
        _context.TravelConnectors.AddRange(travelConnectorAB, travelConnectorBA);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ConsumesTheTicketAndReturnsTravelTime_WhenTheCaravanIsStillPresent()
    {
        // Arrange
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(_caravan.Id, _player.Id, LocationA, LocationB)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — boards right as the caravan arrives (1 full linger hour left) plus the 2-hour
        // leg to the destination, so the caravan's own schedule lands it there exactly on arrival.
        Assert.Equal(BoardCaravanOutcome.Boarded, result.Outcome);
        Assert.Equal(LocationB, result.DestinationLocationId);
        Assert.Equal(3, result.TravelTimeHours);

        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.CaravanTickets.AnyAsync(
                t => t.CreatureId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ReturnsTravelTime_ThatLandsTheCaravanAtTheDestinationOnArrival()
    {
        // Arrange — regression test: the returned travel time must advance playtime by enough
        // that the caravan's own independent, playtime-driven schedule has it lingering at the
        // destination the instant the player is teleported there, not still mid-journey.
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(_caravan.Id, _player.Id, LocationA, LocationB)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolvePosition = _serviceProvider.GetRequiredService<
            IQueryHandler<ResolveRouteTravelerPositionQuery, RouteTimelinePosition?>
        >();

        // Act
        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var arrivalPlaytime = GameClock.RealTimePerInGameHour * result.TravelTimeHours!.Value;
        var position = await resolvePosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = _caravan.Id,
                Playtime = arrivalPlaytime,
            },
            TestContext.Current.CancellationToken
        );
        var lingering = Assert.IsType<RouteTimelinePosition.Lingering>(position);
        Assert.Equal(LocationB, lingering.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsNoTicket_WhenThePlayerNeverPurchasedOne()
    {
        // Act
        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BoardCaravanOutcome.NoTicket, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsCaravanNotPresent_AndKeepsTheTicket_WhenThePlayerLeftTheOriginStop()
    {
        // Arrange
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(_caravan.Id, _player.Id, LocationA, LocationB)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationB,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(BoardCaravanOutcome.CaravanNotPresent, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.CaravanTickets.AnyAsync(
                t => t.CreatureId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_StillBoards_WhenTimeHasPassedSincePurchase_ButThePlayerNeverLeft()
    {
        // Arrange — simulates ordinary narration-time overhead (every narrated turn advances
        // playtime a little) accruing between buying the ticket and clicking Board; the caravan's
        // own live position would already look departed, but the player never left the stop.
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(
                _caravan.Id,
                _player.Id,
                LocationA,
                LocationB,
                purchasedAtPlaytime: TimeSpan.Zero
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationA,
                Playtime = GameClock.RealTimePerInGameHour * 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the ideal 3-hour trip (computed at purchase) shrinks by however much has
        // already drifted, so the destination arrival stays in sync with the caravan's schedule.
        Assert.Equal(BoardCaravanOutcome.Boarded, result.Outcome);
        Assert.Equal(LocationB, result.DestinationLocationId);
        Assert.Equal(1, result.TravelTimeHours);
    }

    [Fact]
    public async Task Handle_ReturnsTravelSuspended_AndKeepsTicket_DuringStorm()
    {
        var stateId = Guid.NewGuid();
        var location = Builders.MakeLocation(WorldId, stateId: stateId, id: LocationA);
        var weather = new WeatherState
        {
            WorldId = WorldId,
            StateId = stateId,
            Condition = WeatherCondition.Storm,
            NextChangePlaytime = TimeSpan.FromHours(1),
        };
        _context.Locations.Add(location);
        _context.WeatherStates.Add(weather);
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(_caravan.Id, _player.Id, LocationA, LocationB)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new BoardCaravanCommand
            {
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(BoardCaravanOutcome.TravelSuspended, result.Outcome);
        Assert.True(
            await _context.CaravanTickets.AnyAsync(
                ticket => ticket.CreatureId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );

        _context.WeatherStates.Remove(weather);
        _context.Locations.Remove(location);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
