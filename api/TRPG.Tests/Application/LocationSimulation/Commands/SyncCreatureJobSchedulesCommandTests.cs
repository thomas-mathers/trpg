using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncCreatureJobSchedulesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_ContinuesFromDirectedArrivalToWork_WithoutTeleporting()
    {
        var origin = Builders.MakeLocation(_worldId);
        var intermediate = Builders.MakeLocation(_worldId);
        var workplace = Builders.MakeLocation(_worldId);
        var creature = Builders.MakeCreature(_worldId, locationId: origin.Id);
        creature.MovementSpeed = 5;
        var firstConnector = Connector(origin.Id, intermediate.Id);
        var secondConnector = Connector(intermediate.Id, workplace.Id);
        _context.Locations.AddRange(origin, intermediate, workplace);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.AddRange(firstConnector, secondConnector);
        _context.TravelConnectors.AddRange(Travel(firstConnector, 5), Travel(secondConnector, 5));
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Work,
                startHour: 10,
                endHour: 18,
                locationId: workplace.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var startRoute = _services.GetRequiredService<
            ICommandHandler<RouteCreatureToDestinationCommand, RouteCreatureToDestinationResult>
        >();
        await startRoute.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = intermediate.Id,
                Playtime = TimeSpan.Zero,
                Purpose = "Following an instruction",
            },
            TestContext.Current.CancellationToken
        );

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        var betweenLegs = GameClock.RealTimePerInGameHour * 1.5;
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                Playtime = betweenLegs,
            },
            TestContext.Current.CancellationToken
        );

        await using (var verifyWalking = db.CreateContext())
        {
            var walking = await verifyWalking.Creatures.SingleAsync(
                entry => entry.Id == creature.Id,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(intermediate.Id, walking.LocationId);
            Assert.Equal(CreatureState.Walking, walking.State);

            var membership = await verifyWalking.RouteTravelerMembers.SingleAsync(
                entry => entry.CreatureId == creature.Id,
                TestContext.Current.CancellationToken
            );
            var traveler = await verifyWalking.RouteTravelers.SingleAsync(
                entry => entry.Id == membership.RouteTravelerId,
                TestContext.Current.CancellationToken
            );
            Assert.Equal("Walking to work", traveler.Purpose);
            Assert.Equal(GameClock.RealTimePerInGameHour, traveler.StartedAtPlaytime);
        }

        var afterWorkArrival = GameClock.RealTimePerInGameHour * 2.5;
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                Playtime = afterWorkArrival,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyWorking = db.CreateContext();
        var working = await verifyWorking.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(workplace.Id, working.LocationId);
        Assert.Equal(CreatureState.Busy, working.State);
        Assert.DoesNotContain(
            await verifyWorking.RouteTravelerMembers.ToArrayAsync(
                TestContext.Current.CancellationToken
            ),
            entry => entry.CreatureId == creature.Id
        );
    }

    private LocationConnector Connector(Guid originLocationId, Guid destinationLocationId) =>
        new()
        {
            WorldId = _worldId,
            OriginLocationId = originLocationId,
            DestinationLocationId = destinationLocationId,
            DestinationLabel = "Destination",
        };

    private TravelConnector Travel(LocationConnector connector, float distance) =>
        new()
        {
            WorldId = _worldId,
            ConnectorId = connector.Id,
            Distance = distance,
        };
}
