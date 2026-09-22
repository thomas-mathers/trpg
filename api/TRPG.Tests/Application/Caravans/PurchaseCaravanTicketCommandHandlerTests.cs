using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Caravans.Commands;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Caravans;

public sealed class PurchaseCaravanTicketCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationA = Guid.NewGuid();
    private static readonly Guid LocationB = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PurchaseCaravanTicketCommandHandler _handler = null!;
    private Route _route = null!;
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
        _handler = _serviceProvider.GetRequiredService<PurchaseCaravanTicketCommandHandler>();

        _route = Builders.MakeCaravanRoute(WorldId, lingerHours: 1);
        var stopA = Builders.MakeCaravanRouteStop(_route.Id, 0, LocationA, distanceToNextStop: 10);
        var stopB = Builders.MakeCaravanRouteStop(_route.Id, 1, LocationB, distanceToNextStop: 10);
        var fare = Builders.MakeCaravanFare(_route.Id, WorldId, ticketFeeGold: 10);
        _caravan = Builders.MakeCaravan(_route.Id, WorldId);
        _player = Builders.MakeCreature(worldId: WorldId, locationId: LocationA);

        _context.Routes.Add(_route);
        _context.RouteStops.AddRange(stopA, stopB);
        _context.CaravanFares.Add(fare);
        _context.RouteTravelers.Add(_caravan);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ChargesGoldAndCreatesATicket_WhenTheCaravanIsPresentAndAffordable()
    {
        // Arrange
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 10,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CaravanId = _caravan.Id,
                DestinationLocationId = LocationB,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(PurchaseCaravanTicketOutcome.Purchased, result.Outcome);
        Assert.Equal(10, result.GoldCharged);

        var updatedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                i => i.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(0, updatedGold.Quantity);

        var ticket = await verifyContext.CaravanTickets.SingleAsync(
            t => t.CreatureId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_caravan.Id, ticket.RouteTravelerId);
        Assert.Equal(LocationA, ticket.OriginStopLocationId);
        Assert.Equal(LocationB, ticket.DestinationLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsInsufficientGold_AndCreatesNoTicket_WhenPlayerCannotAfford()
    {
        // Arrange
        var gold = Builders.MakeGold(
            WorldId,
            quantity: 2,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CaravanId = _caravan.Id,
                DestinationLocationId = LocationB,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(PurchaseCaravanTicketOutcome.InsufficientGold, result.Outcome);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.CaravanTickets.AnyAsync(
                t => t.CreatureId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ReturnsCaravanNotPresent_WhenTheCaravanIsInTransit()
    {
        // Act
        var result = await _handler.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CaravanId = _caravan.Id,
                DestinationLocationId = LocationB,
                PlayerLocationId = LocationA,
                Playtime = GameClock.RealTimePerInGameHour * 2,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(PurchaseCaravanTicketOutcome.CaravanNotPresent, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidDestination_WhenTheLocationIsNotARouteStop()
    {
        // Act
        var result = await _handler.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CaravanId = _caravan.Id,
                DestinationLocationId = Guid.NewGuid(),
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(PurchaseCaravanTicketOutcome.InvalidDestination, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyHoldsTicket_WhenThePlayerAlreadyHasOneForThisCaravan()
    {
        // Arrange
        _context.CaravanTickets.Add(
            Builders.MakeCaravanTicket(_caravan.Id, _player.Id, LocationA, LocationB)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new PurchaseCaravanTicketCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CaravanId = _caravan.Id,
                DestinationLocationId = LocationB,
                PlayerLocationId = LocationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(PurchaseCaravanTicketOutcome.AlreadyHoldsTicket, result.Outcome);
    }
}
