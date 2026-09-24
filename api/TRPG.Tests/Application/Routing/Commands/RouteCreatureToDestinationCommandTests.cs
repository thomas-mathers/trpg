using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Routing.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing.Commands;

public sealed class RouteCreatureToDestinationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private RouteCreatureToDestinationCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<RouteCreatureToDestinationCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DerivesOriginWorldAndSpeed_FromTheCreature()
    {
        var worldId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(worldId, locationId: originId);
        creature.MovementSpeed = 7;
        var connector = AddMeasuredConnector(worldId, originId, destinationId, distance: 14);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = destinationId,
                Playtime = GameClock.RealTimePerInGameHour * 3,
                Purpose = "Going to work.",
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var traveler = await _context.RouteTravelers.SingleAsync(
            entry => entry.Id == result.RouteTravelerId,
            TestContext.Current.CancellationToken
        );
        var steps = await _context
            .RouteSteps.Where(step => step.RouteId == traveler.RouteId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var updatedCreature = await _context.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(worldId, traveler.WorldId);
        Assert.Equal(7, traveler.SpeedUnitsPerHour);
        Assert.Equal(GameClock.RealTimePerInGameHour * 3, traveler.StartedAtPlaytime);
        Assert.Equal("Going to work.", traveler.Purpose);
        Assert.Equal([connector.Id, null], steps.Select(step => step.ConnectorId));
        Assert.Equal([originId, destinationId], steps.Select(step => step.LocationId));
        Assert.Equal(CreatureState.Walking, updatedCreature.State);
    }

    [Fact]
    public async Task Handle_FinishesTheCurrentLegBeforeReturningThroughItsOrigin_WhenPreempted()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var outbound = AddMeasuredConnector(worldId, locationX, locationY, distance: 10);
        var inbound = AddMeasuredConnector(worldId, locationY, locationX, distance: 10);
        var creature = Builders.MakeCreature(worldId, locationId: locationX);
        creature.MovementSpeed = 20;
        var traveler = AddFiniteTraveler(creature, speedUnitsPerHour: 10, [outbound], locationY);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var decisionPlaytime = GameClock.RealTimePerInGameHour * 0.4;

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = locationX,
                Playtime = decisionPlaytime,
                Purpose = "Returning to the gate.",
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var replacement = await _context.RouteTravelers.SingleAsync(
            entry => entry.Id == result.RouteTravelerId,
            TestContext.Current.CancellationToken
        );
        var steps = await _context
            .RouteSteps.Where(step => step.RouteId == replacement.RouteId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var position = await Resolve(replacement.Id, decisionPlaytime);

        Assert.Equal(traveler.StartedAtPlaytime, replacement.StartedAtPlaytime);
        Assert.Equal(10, replacement.SpeedUnitsPerHour);
        Assert.Equal([outbound.Id, inbound.Id, null], steps.Select(step => step.ConnectorId));
        var inTransit = Assert.IsType<RouteTimelinePosition.InTransit>(position.Position);
        Assert.Equal(outbound.Id, inTransit.ConnectorId);
        Assert.Equal(0.6, inTransit.HoursUntilArrival, precision: 10);
    }

    [Fact]
    public async Task Handle_MaterializesACompletedArrival_BeforeStartingTheReplacement()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var locationZ = Guid.NewGuid();
        var first = AddMeasuredConnector(worldId, locationX, locationY, distance: 10);
        var second = AddMeasuredConnector(worldId, locationY, locationZ, distance: 5);
        var creature = Builders.MakeCreature(worldId, locationId: locationX);
        creature.MovementSpeed = 5;
        AddFiniteTraveler(creature, speedUnitsPerHour: 10, [first], locationY);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = locationZ,
                Playtime = GameClock.RealTimePerInGameHour * 2,
                Purpose = "Continuing to the market.",
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var updatedCreature = await _context.Creatures.SingleAsync(
            entry => entry.Id == creature.Id,
            TestContext.Current.CancellationToken
        );
        var replacement = await _context.RouteTravelers.SingleAsync(
            entry => entry.Id == result.RouteTravelerId,
            TestContext.Current.CancellationToken
        );
        var firstStep = await _context.RouteSteps.SingleAsync(
            step => step.RouteId == replacement.RouteId && step.SequenceIndex == 0,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(locationY, updatedCreature.LocationId);
        Assert.Equal(CreatureState.Walking, updatedCreature.State);
        Assert.Equal(locationY, firstStep.LocationId);
        Assert.Equal(second.Id, firstStep.ConnectorId);
        Assert.Equal(GameClock.RealTimePerInGameHour * 2, replacement.StartedAtPlaytime);
    }

    private LocationConnector AddMeasuredConnector(
        Guid worldId,
        Guid originLocationId,
        Guid destinationLocationId,
        float distance
    )
    {
        var connector = Builders.MakeLocationConnector(
            originLocationId,
            destinationLocationId,
            worldId
        );
        _context.LocationConnectors.Add(connector);
        _context.TravelConnectors.Add(
            Builders.MakeTravelConnector(connector.Id, distance, worldId: worldId)
        );
        return connector;
    }

    private RouteTraveler AddFiniteTraveler(
        Creature creature,
        double speedUnitsPerHour,
        IReadOnlyList<LocationConnector> connectors,
        Guid destinationLocationId
    )
    {
        var route = new Route
        {
            WorldId = creature.WorldId,
            Name = "Existing journey",
            Traversal = RouteTraversal.Finite,
        };
        var traveler = new RouteTraveler
        {
            WorldId = creature.WorldId,
            RouteId = route.Id,
            StartedAtPlaytime = TimeSpan.Zero,
            SpeedUnitsPerHour = speedUnitsPerHour,
            Purpose = "Original destination.",
        };
        _context.Creatures.Add(creature);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            connectors.Select(
                (connector, index) =>
                    new RouteStep
                    {
                        WorldId = creature.WorldId,
                        RouteId = route.Id,
                        SequenceIndex = index,
                        LocationId = connector.OriginLocationId,
                        ConnectorId = connector.Id,
                        DwellHours = 0,
                    }
            )
        );
        _context.RouteSteps.Add(
            new RouteStep
            {
                WorldId = creature.WorldId,
                RouteId = route.Id,
                SequenceIndex = connectors.Count,
                LocationId = destinationLocationId,
                ConnectorId = null,
                DwellHours = 0,
            }
        );
        _context.RouteTravelers.Add(traveler);
        _context.RouteTravelerMembers.Add(
            Builders.MakeRouteTravelerMember(traveler.Id, creature.Id, creature.WorldId)
        );
        return traveler;
    }

    private async Task<ResolvedRouteTravelerPosition> Resolve(Guid travelerId, TimeSpan playtime)
    {
        var handler = _services.GetRequiredService<ResolveRouteTravelerPositionsQueryHandler>();
        var positions = await handler.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = [travelerId],
                Playtime = playtime,
            },
            TestContext.Current.CancellationToken
        );
        return positions[travelerId];
    }
}
