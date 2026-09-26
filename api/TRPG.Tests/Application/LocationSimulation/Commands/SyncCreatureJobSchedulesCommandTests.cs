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
                GameTime = GameClock.Epoch,
                Purpose = "Following an instruction",
            },
            TestContext.Current.CancellationToken
        );

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        var betweenLegs = GameClock.Epoch + TimeSpan.FromHours(1.5);
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = betweenLegs,
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
            Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(1), traveler.StartedAtGameTime);
        }

        var afterWorkArrival = GameClock.Epoch + TimeSpan.FromHours(2.5);
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = afterWorkArrival,
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

    [Fact]
    public async Task Handle_BackdatesAnUpcomingCommute_ToReachWorkOnTime()
    {
        var home = Builders.MakeLocation(_worldId);
        var workplace = Builders.MakeLocation(_worldId);
        var creature = Builders.MakeCreature(_worldId, locationId: home.Id);
        creature.MovementSpeed = 5;
        var connector = Connector(home.Id, workplace.Id);
        _context.Locations.AddRange(home, workplace);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.Add(connector);
        _context.TravelConnectors.Add(Travel(connector, 5));
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

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 1.5,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var travelerId = await verify
            .RouteTravelerMembers.Where(member => member.CreatureId == creature.Id)
            .Select(member => member.RouteTravelerId)
            .SingleAsync(TestContext.Current.CancellationToken);
        var traveler = await verify.RouteTravelers.SingleAsync(
            entry => entry.Id == travelerId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(1), traveler.StartedAtGameTime);
        var walking = await verify.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Walking, walking!.State);
    }

    [Fact]
    public async Task Handle_MaterializesAWorkerAtWork_WhenFirstSynchronizedAfterArrival()
    {
        var home = Builders.MakeLocation(_worldId);
        var workplace = Builders.MakeLocation(_worldId);
        var creature = Builders.MakeCreature(_worldId, locationId: home.Id);
        creature.MovementSpeed = 5;
        var connector = Connector(home.Id, workplace.Id);
        _context.Locations.AddRange(home, workplace);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.Add(connector);
        _context.TravelConnectors.Add(Travel(connector, 5));
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

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 3,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var worker = await verify.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(workplace.Id, worker.LocationId);
        Assert.Equal(CreatureState.Busy, worker.State);
        Assert.DoesNotContain(
            await verify.RouteTravelerMembers.ToArrayAsync(TestContext.Current.CancellationToken),
            member => member.CreatureId == creature.Id
        );
    }

    [Fact]
    public async Task Handle_AssignsLimitedSeating_InArrivalOrderForIdleVisitors()
    {
        var location = Builders.MakeLocation(_worldId);
        var firstArrival = Builders.MakeCreature(_worldId, locationId: location.Id);
        var secondArrival = Builders.MakeCreature(_worldId, locationId: location.Id);
        var seat = Builders.MakeSeat(_worldId, location.Id);
        _context.Locations.Add(location);
        _context.Creatures.AddRange(firstArrival, secondArrival);
        _context.Props.Add(seat);
        _context.CreatureJobs.AddRange(
            Builders.MakeCreatureJob(
                firstArrival.Id,
                action: CreatureJobAction.Idle,
                startHour: 0,
                endHour: 23,
                locationId: location.Id,
                worldId: _worldId
            ),
            Builders.MakeCreatureJob(
                secondArrival.Id,
                action: CreatureJobAction.Idle,
                startHour: 0,
                endHour: 23,
                locationId: location.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();

        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [secondArrival.Id, firstArrival.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 3,
                BecameAvailableAtGameTimeByCreatureId = new Dictionary<Guid, GameInstant>
                {
                    [firstArrival.Id] = GameClock.Epoch + TimeSpan.FromHours(1),
                    [secondArrival.Id] = GameClock.Epoch + TimeSpan.FromHours(2),
                },
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var creatures = await verify
            .Creatures.Where(creature =>
                creature.Id == firstArrival.Id || creature.Id == secondArrival.Id
            )
            .ToDictionaryAsync(creature => creature.Id, TestContext.Current.CancellationToken);
        var occupiedSeat = await verify
            .Props.OfType<Seat>()
            .SingleAsync(prop => prop.Id == seat.Id, TestContext.Current.CancellationToken);
        Assert.Equal(firstArrival.Id, occupiedSeat.OccupantId);
        Assert.Equal(CreatureState.Sitting, creatures[firstArrival.Id].State);
        Assert.Equal(CreatureState.Idle, creatures[secondArrival.Id].State);
    }

    [Fact]
    public async Task Handle_StartsAnActiveRouteBackedJob_AtTheShiftBoundary()
    {
        var firstDistrict = Builders.MakeLocation(_worldId);
        var secondDistrict = Builders.MakeLocation(_worldId);
        var creature = Builders.MakeCreature(_worldId, locationId: firstDistrict.Id);
        creature.MovementSpeed = 5;
        var outbound = Connector(firstDistrict.Id, secondDistrict.Id);
        var inbound = Connector(secondDistrict.Id, firstDistrict.Id);
        var route = new Route
        {
            WorldId = _worldId,
            Name = "City patrol",
            Traversal = RouteTraversal.Cyclic,
        };
        _context.Locations.AddRange(firstDistrict, secondDistrict);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.AddRange(outbound, inbound);
        _context.TravelConnectors.AddRange(Travel(outbound, 5), Travel(inbound, 5));
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            PatrolStep(route.Id, 0, firstDistrict.Id, outbound.Id),
            PatrolStep(route.Id, 1, secondDistrict.Id, inbound.Id)
        );
        _context.CreatureJobs.Add(
            new CreatureJob
            {
                WorldId = _worldId,
                CreatureId = creature.Id,
                Action = CreatureJobAction.Work,
                StartHour = 8,
                EndHour = 18,
                Priority = 1,
                LocationId = firstDistrict.Id,
                RouteId = route.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var membership = await verify.RouteTravelerMembers.SingleAsync(
            member => member.CreatureId == creature.Id,
            TestContext.Current.CancellationToken
        );
        var traveler = await verify.RouteTravelers.SingleAsync(
            entry => entry.Id == membership.RouteTravelerId,
            TestContext.Current.CancellationToken
        );
        var guard = await verify.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(route.Id, traveler.RouteId);
        Assert.Equal(GameClock.Epoch, traveler.StartedAtGameTime);
        Assert.Equal(CreatureState.Busy, guard.State);
    }

    [Fact]
    public async Task Handle_PreemptsAPatrolAtTheShiftEnd_BeforeRoutingToTheNextJob()
    {
        var firstDistrict = Builders.MakeLocation(_worldId);
        var secondDistrict = Builders.MakeLocation(_worldId);
        var creature = Builders.MakeCreature(_worldId, locationId: firstDistrict.Id);
        creature.MovementSpeed = 5;
        var outbound = Connector(firstDistrict.Id, secondDistrict.Id);
        var inbound = Connector(secondDistrict.Id, firstDistrict.Id);
        var route = new Route
        {
            WorldId = _worldId,
            Name = "City patrol",
            Traversal = RouteTraversal.Cyclic,
        };
        var traveler = new RouteTraveler
        {
            WorldId = _worldId,
            RouteId = route.Id,
            StartedAtGameTime = GameClock.Epoch,
            SpeedUnitsPerHour = creature.MovementSpeed,
            Purpose = "Patrolling the city",
        };
        _context.Locations.AddRange(firstDistrict, secondDistrict);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.AddRange(outbound, inbound);
        _context.TravelConnectors.AddRange(Travel(outbound, 5), Travel(inbound, 5));
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            PatrolStep(route.Id, 0, firstDistrict.Id, outbound.Id),
            PatrolStep(route.Id, 1, secondDistrict.Id, inbound.Id)
        );
        _context.RouteTravelers.Add(traveler);
        _context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = _worldId,
                RouteTravelerId = traveler.Id,
                CreatureId = creature.Id,
            }
        );
        _context.CreatureJobs.AddRange(
            new CreatureJob
            {
                WorldId = _worldId,
                CreatureId = creature.Id,
                Action = CreatureJobAction.Work,
                StartHour = 8,
                EndHour = 9,
                Priority = 1,
                LocationId = firstDistrict.Id,
                RouteId = route.Id,
            },
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Sleep,
                startHour: 9,
                endHour: 17,
                locationId: secondDistrict.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var guard = await verify.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(secondDistrict.Id, guard.LocationId);
        Assert.Equal(CreatureState.Sleeping, guard.State);
        Assert.DoesNotContain(
            await verify.RouteTravelerMembers.ToArrayAsync(TestContext.Current.CancellationToken),
            member => member.CreatureId == creature.Id
        );
    }

    [Fact]
    public async Task Handle_RoutesOutdoorIdleCreatureHome_DuringSevereWeather()
    {
        var districtId = Guid.NewGuid();
        var outdoors = Builders.MakeLocation(
            _worldId,
            districtId: districtId,
            kind: LocationKind.District
        );
        var home = Builders.MakeLocation(_worldId, roomId: Guid.NewGuid());
        var creature = Builders.MakeCreature(
            _worldId,
            profession: Profession.Baker,
            locationId: outdoors.Id
        );
        creature.MovementSpeed = 5;
        var outbound = Connector(outdoors.Id, home.Id);
        var inbound = Connector(home.Id, outdoors.Id);
        _context.Locations.AddRange(outdoors, home);
        _context.Creatures.Add(creature);
        _context.LocationConnectors.AddRange(outbound, inbound);
        _context.TravelConnectors.AddRange(Travel(outbound, 5), Travel(inbound, 5));
        _context.CreatureJobs.AddRange(
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Idle,
                startHour: 6,
                endHour: 22,
                locationId: outdoors.Id,
                worldId: _worldId
            ),
            Builders.MakeCreatureJob(
                creature.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                locationId: home.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [creature.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
                Weather = WeatherCondition.Snow,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var sheltered = await verify.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(home.Id, sheltered.LocationId);
        Assert.Equal(CreatureState.Idle, sheltered.State);
    }

    [Fact]
    public async Task Handle_DoesNotShelterGuard_DuringSevereWeather()
    {
        var outdoors = Builders.MakeLocation(_worldId, districtId: Guid.NewGuid());
        var home = Builders.MakeLocation(_worldId, roomId: Guid.NewGuid());
        var guard = Builders.MakeCreature(
            _worldId,
            profession: Profession.Guard,
            locationId: outdoors.Id
        );
        _context.Locations.AddRange(outdoors, home);
        _context.Creatures.Add(guard);
        _context.CreatureJobs.AddRange(
            Builders.MakeCreatureJob(
                guard.Id,
                action: CreatureJobAction.Idle,
                startHour: 6,
                endHour: 22,
                locationId: outdoors.Id,
                worldId: _worldId
            ),
            Builders.MakeCreatureJob(
                guard.Id,
                action: CreatureJobAction.Sleep,
                startHour: 22,
                endHour: 6,
                locationId: home.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var synchronize = _services.GetRequiredService<
            ICommandHandler<SyncCreatureJobSchedulesCommand, SyncCreatureJobSchedulesResult>
        >();
        await synchronize.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [guard.Id],
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
                Weather = WeatherCondition.Snow,
            },
            TestContext.Current.CancellationToken
        );

        await using var verify = db.CreateContext();
        var onDuty = await verify.Creatures.SingleAsync(
            entry => entry.Id == guard.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(outdoors.Id, onDuty.LocationId);
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

    private RouteStep PatrolStep(
        Guid routeId,
        int sequenceIndex,
        Guid locationId,
        Guid connectorId
    ) =>
        new()
        {
            WorldId = _worldId,
            RouteId = routeId,
            SequenceIndex = sequenceIndex,
            LocationId = locationId,
            ConnectorId = connectorId,
            DwellHours = 0.5,
        };
}
