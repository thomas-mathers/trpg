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

public sealed class SyncGuardPatrolCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationA = Guid.NewGuid();
    private readonly Guid _locationB = Guid.NewGuid();
    private readonly Guid _locationC = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncGuardPatrolCommandHandler _handler = null!;
    private Route _route = null!;
    private RouteTraveler _traveler = null!;
    private Creature _guard1 = null!;
    private Creature _guard2 = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CountryPatrolOptions>>(
                new TestOptionsSnapshot<CountryPatrolOptions>(
                    new CountryPatrolOptions { SpeedUnitsPerHour = 5 }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncGuardPatrolCommandHandler>();

        // 2 stops, 10 units apart each way at speed 5 = 2 leg hours; with a 1-hour linger the
        // total cycle is 2 * (1 + 2) = 6 hours, and stop A's own linger window is [0, 1).
        _route = Builders.MakeCaravanRoute(_worldId, lingerHours: 1);
        var stopA = Builders.MakeCaravanRouteStop(_route.Id, 0, _locationA, distanceToNextStop: 10);
        var stopB = Builders.MakeCaravanRouteStop(_route.Id, 1, _locationB, distanceToNextStop: 10);
        _traveler = Builders.MakeCaravan(
            _route.Id,
            _worldId,
            phaseOffsetHours: 0,
            kind: RouteTravelerKind.GuardPatrol,
            purpose: "Patrolling the roads."
        );
        _guard1 = Builders.MakeCreature(_worldId, profession: Profession.Guard);
        _guard2 = Builders.MakeCreature(_worldId, profession: Profession.Guard);

        _context.Routes.Add(_route);
        _context.RouteStops.AddRange(stopA, stopB);
        _context.RouteTravelers.Add(_traveler);
        _context.Creatures.AddRange(_guard1, _guard2);
        _context.RouteTravelerMembers.AddRange(
            Builders.MakeRouteTravelerMember(_traveler.Id, _guard1.Id, _worldId),
            Builders.MakeRouteTravelerMember(_traveler.Id, _guard2.Id, _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RelocatesEveryPatrolMember_WhenTheirTravelerIsLingeringHere()
    {
        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var guards = await verifyContext
            .Creatures.Where(c => c.Id == _guard1.Id || c.Id == _guard2.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(
            guards,
            guard =>
            {
                Assert.Equal(_locationA, guard.LocationId);
                Assert.Equal(CreatureState.Idle, guard.State);
            }
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoRouteTouchesTheLocation()
    {
        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationC,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var guard = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _guard1.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_guard1.LocationId, guard.LocationId);
    }

    [Fact]
    public async Task Handle_RelocatesTheWholeSquadToTheDepartureStop_WhenInTransitToTheNextOne()
    {
        // Arrange — stop A's linger window is [0, 1); both guards are still (stale) shown at B
        // from an earlier leg, but at exactly 1 in-game hour in, the traveler is now InTransit
        // from A toward B — a squad in transit is attributed to the stop it just left, not the
        // one it's heading toward, so it belongs back at A.
        _guard1.LocationId = _locationB;
        _guard2.LocationId = _locationB;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationA,
                Playtime = GameClock.RealTimePerInGameHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the whole squad relocates together, none left behind at B, and both are now
        // flagged as mid-patrol rather than genuinely stationed here.
        await using var verifyContext = db.CreateContext();
        var guards = await verifyContext
            .Creatures.Where(c => c.Id == _guard1.Id || c.Id == _guard2.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(
            guards,
            guard =>
            {
                Assert.Equal(_locationA, guard.LocationId);
                Assert.Equal(CreatureState.Patrolling, guard.State);
            }
        );
    }

    [Fact]
    public async Task Handle_CorrectsAStaleState_EvenWhenTheLocationAlreadyMatches()
    {
        // Arrange — guard1 is already at stop A (e.g. its world-gen starting spot), but still
        // carries the default Idle state; 1 hour in, the traveler is InTransit from A toward B, so
        // the location needs no change but the state is stale and must still be corrected.
        _guard1.LocationId = _locationA;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationA,
                Playtime = GameClock.RealTimePerInGameHour,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var guard = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _guard1.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_locationA, guard.LocationId);
        Assert.Equal(CreatureState.Patrolling, guard.State);
    }

    [Fact]
    public async Task Handle_SkipsDeadGuards()
    {
        // Arrange
        _guard1.State = CreatureState.Dead;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var deadGuardOriginalLocationId = _guard1.LocationId;

        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var deadGuard = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _guard1.Id,
            TestContext.Current.CancellationToken
        );
        var livingGuard = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _guard2.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(deadGuardOriginalLocationId, deadGuard.LocationId);
        Assert.Equal(_locationA, livingGuard.LocationId);
    }

    [Fact]
    public async Task Handle_LeavesPreviousLocationUntouched_WhenAGuardIsAlreadyAtTheLocation()
    {
        // Arrange — already standing at stop A, so the sync should not touch this creature at all.
        _guard1.LocationId = _locationA;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncGuardPatrolCommand
            {
                WorldId = _worldId,
                LocationId = _locationA,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var guard = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _guard1.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(guard.PreviousLocationId);
    }
}
