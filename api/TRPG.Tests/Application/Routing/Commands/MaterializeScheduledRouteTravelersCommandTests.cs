using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing.Commands;

public sealed class MaterializeScheduledRouteTravelersCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<
        MaterializeScheduledRouteTravelersCommand,
        IReadOnlyCollection<Guid>
    > _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<
            ICommandHandler<MaterializeScheduledRouteTravelersCommand, IReadOnlyCollection<Guid>>
        >();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MaterializesCurrentOccurrence_WhenCreatureIsDepartingLocation()
    {
        var scenario = await AddScenario();

        var creatureIds = await _handler.Handle(
            new MaterializeScheduledRouteTravelersCommand
            {
                WorldId = scenario.WorldId,
                LocationId = scenario.MiddleLocationId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var traveler = await _context.RouteTravelers.SingleAsync(
            entry => entry.CreatureRouteScheduleId == scenario.ScheduleId,
            TestContext.Current.CancellationToken
        );
        var member = await _context.RouteTravelerMembers.SingleAsync(
            entry => entry.RouteTravelerId == traveler.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal([scenario.CreatureId], creatureIds);
        Assert.Equal(scenario.ScheduleId, traveler.CreatureRouteScheduleId);
        Assert.Equal(-GameClock.RealTimePerInGameHour, traveler.StartedAtPlaytime);
        Assert.Equal(scenario.CreatureId, member.CreatureId);
    }

    [Fact]
    public async Task Handle_ReplacesStaleOccurrence_WithCurrentOccurrence()
    {
        var scenario = await AddScenario();
        var staleTraveler = new RouteTraveler
        {
            WorldId = scenario.WorldId,
            RouteId = scenario.RouteId,
            CreatureRouteScheduleId = scenario.ScheduleId,
            StartedAtPlaytime = -GameClock.RealTimePerInGameHour * 169,
            SpeedUnitsPerHour = 5,
            Purpose = "Walking to work",
        };
        _context.RouteTravelers.Add(staleTraveler);
        _context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = scenario.WorldId,
                RouteTravelerId = staleTraveler.Id,
                CreatureId = scenario.CreatureId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _handler.Handle(
            new MaterializeScheduledRouteTravelersCommand
            {
                WorldId = scenario.WorldId,
                LocationId = scenario.MiddleLocationId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var traveler = await _context.RouteTravelers.SingleAsync(
            entry => entry.CreatureRouteScheduleId == scenario.ScheduleId,
            TestContext.Current.CancellationToken
        );
        Assert.NotEqual(staleTraveler.Id, traveler.Id);
        Assert.Equal(-GameClock.RealTimePerInGameHour, traveler.StartedAtPlaytime);
    }

    [Fact]
    public async Task Handle_RemovesStaleOccurrence_WhenCurrentJourneyAlreadyFinished()
    {
        var scenario = await AddScenario(departureHour: 5, destinationStartHour: 7);
        var staleTraveler = new RouteTraveler
        {
            WorldId = scenario.WorldId,
            RouteId = scenario.RouteId,
            CreatureRouteScheduleId = scenario.ScheduleId,
            StartedAtPlaytime = -GameClock.RealTimePerInGameHour * 171,
            SpeedUnitsPerHour = 5,
            Purpose = "Walking to work",
        };
        _context.RouteTravelers.Add(staleTraveler);
        _context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = scenario.WorldId,
                RouteTravelerId = staleTraveler.Id,
                CreatureId = scenario.CreatureId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var creatureIds = await _handler.Handle(
            new MaterializeScheduledRouteTravelersCommand
            {
                WorldId = scenario.WorldId,
                LocationId = scenario.MiddleLocationId,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        Assert.Empty(creatureIds);
        Assert.DoesNotContain(
            await _context.RouteTravelers.ToArrayAsync(TestContext.Current.CancellationToken),
            traveler => traveler.CreatureRouteScheduleId == scenario.ScheduleId
        );
        Assert.DoesNotContain(
            await _context.RouteTravelerMembers.ToArrayAsync(TestContext.Current.CancellationToken),
            member => member.CreatureId == scenario.CreatureId
        );
        var creature = await _context.Creatures.SingleAsync(
            entry => entry.Id == scenario.CreatureId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(scenario.DestinationLocationId, creature.LocationId);
    }

    private async Task<Scenario> AddScenario(double departureHour = 7, int destinationStartHour = 9)
    {
        var worldId = Guid.NewGuid();
        var origin = Builders.MakeLocation(worldId);
        var middle = Builders.MakeLocation(worldId);
        var destination = Builders.MakeLocation(worldId);
        var creature = Builders.MakeCreature(worldId, locationId: origin.Id);
        creature.MovementSpeed = 5;
        var firstConnector = Connector(worldId, origin.Id, middle.Id);
        var secondConnector = Connector(worldId, middle.Id, destination.Id);
        var route = new Route
        {
            WorldId = worldId,
            Name = "Home to work",
            Traversal = RouteTraversal.Finite,
        };
        var originJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Sleep,
            startHour: 0,
            endHour: destinationStartHour,
            locationId: origin.Id,
            worldId: worldId
        );
        var destinationJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            startHour: destinationStartHour,
            endHour: 17,
            locationId: destination.Id,
            worldId: worldId
        );
        var schedule = new CreatureRouteSchedule
        {
            WorldId = worldId,
            CreatureId = creature.Id,
            RouteId = route.Id,
            OriginCreatureJobId = originJob.Id,
            DestinationCreatureJobId = destinationJob.Id,
            DepartureDay = GameClock.GetCurrentInGameDateTime(TimeSpan.Zero).DayOfWeek,
            DepartureHour = departureHour,
            DurationHours = 2,
            Purpose = "Walking to work",
        };
        _context.Locations.AddRange(origin, middle, destination);
        _context.Creatures.Add(creature);
        _context.CreatureJobs.AddRange(originJob, destinationJob);
        _context.LocationConnectors.AddRange(firstConnector, secondConnector);
        _context.TravelConnectors.AddRange(
            Travel(worldId, firstConnector.Id),
            Travel(worldId, secondConnector.Id)
        );
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            Step(worldId, route.Id, 0, origin.Id, firstConnector.Id),
            Step(worldId, route.Id, 1, middle.Id, secondConnector.Id),
            Step(worldId, route.Id, 2, destination.Id, null)
        );
        _context.CreatureRouteSchedules.Add(schedule);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Scenario(worldId, creature.Id, middle.Id, destination.Id, route.Id, schedule.Id);
    }

    private static LocationConnector Connector(
        Guid worldId,
        Guid originLocationId,
        Guid destinationLocationId
    ) =>
        new()
        {
            WorldId = worldId,
            OriginLocationId = originLocationId,
            DestinationLocationId = destinationLocationId,
            DestinationLabel = "Destination",
        };

    private static TravelConnector Travel(Guid worldId, Guid connectorId) =>
        new()
        {
            WorldId = worldId,
            ConnectorId = connectorId,
            Distance = 5,
        };

    private static RouteStep Step(
        Guid worldId,
        Guid routeId,
        int index,
        Guid locationId,
        Guid? connectorId
    ) =>
        new()
        {
            WorldId = worldId,
            RouteId = routeId,
            SequenceIndex = index,
            LocationId = locationId,
            ConnectorId = connectorId,
            DwellHours = 0,
        };

    private sealed record Scenario(
        Guid WorldId,
        Guid CreatureId,
        Guid MiddleLocationId,
        Guid DestinationLocationId,
        Guid RouteId,
        Guid ScheduleId
    );
}
