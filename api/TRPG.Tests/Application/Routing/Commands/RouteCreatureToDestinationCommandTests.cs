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
    private RouteCreaturesToDestinationsCommandHandler _batchHandler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<RouteCreatureToDestinationCommandHandler>();
        _batchHandler = _services.GetRequiredService<RouteCreaturesToDestinationsCommandHandler>();
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
        var connector = AddConnector(worldId, originId, destinationId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = destinationId,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 3,
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
        Assert.Equal(WalkPace.MetersFor(7, 1), traveler.SpeedUnitsPerHour);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(1) * 3, traveler.StartedAtGameTime);
        Assert.Equal("Going to work.", traveler.Purpose);
        Assert.Equal([connector.Id, null], steps.Select(step => step.ConnectorId));
        Assert.Equal([originId, destinationId], steps.Select(step => step.LocationId));
        Assert.Equal(CreatureMovement.Walking, updatedCreature.Movement);
    }

    [Fact]
    public async Task Handle_FinishesTheCurrentLegBeforeReturningThroughItsOrigin_WhenPreempted()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var outbound = AddConnector(worldId, locationX, locationY);
        var inbound = AddReverseConnector(outbound);
        var creature = Builders.MakeCreature(worldId, locationId: locationX);
        creature.MovementSpeed = 20;
        var traveler = AddFiniteTraveler(creature, speedUnitsPerHour: 10, [outbound], locationY);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var decisionGameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 0.4;

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = locationX,
                GameTime = decisionGameTime,
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
        var position = await Resolve(replacement.Id, decisionGameTime);

        Assert.Equal(traveler.StartedAtGameTime, replacement.StartedAtGameTime);
        Assert.Equal(10, replacement.SpeedUnitsPerHour);
        Assert.Equal([outbound.Id, inbound.Id, null], steps.Select(step => step.ConnectorId));
        var inTransit = Assert.IsType<RouteTimelinePosition.InTransit>(position.Position);
        Assert.Equal(outbound.Id, inTransit.ConnectorId);
        Assert.Equal(0.6, inTransit.HoursUntilArrival, precision: 10);
    }

    [Fact]
    public async Task Handle_CarriesTheCurrentLegDistanceAndWalksTheTailFromItsArrivalNode_WhenPreempted()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var outbound = AddConnector(worldId, locationX, locationY);
        var inbound = Builders.MakeLocationConnector(locationY, locationX, worldId);
        var inboundExit = Builders.MakeExitNode(inbound);
        var inboundArrival = Builders.MakeArrivalNode(inbound);
        _context.LocationConnectors.Add(inbound);
        _context.TravelNodes.AddRange(inboundExit, inboundArrival);
        _context.PointConnectors.Add(
            Builders.MakePointConnector(
                locationY,
                outbound.DestinationNodeId,
                inboundExit.Id,
                distance: 4,
                worldId
            )
        );
        var creature = Builders.MakeCreature(worldId, locationId: locationX);
        creature.MovementSpeed = 20;
        AddFiniteTraveler(creature, speedUnitsPerHour: 10, [outbound], locationY);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = locationX,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 0.4,
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

        Assert.Equal([outbound.Id, inbound.Id, null], steps.Select(step => step.ConnectorId));
        Assert.Equal([10d, 4d, 0d], steps.Select(step => step.Distance));
    }

    [Fact]
    public async Task Handle_MaterializesACompletedArrival_BeforeStartingTheReplacement()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var locationZ = Guid.NewGuid();
        var first = AddConnector(worldId, locationX, locationY);
        var second = AddConnector(worldId, locationY, locationZ);
        var creature = Builders.MakeCreature(worldId, locationId: locationX);
        creature.MovementSpeed = 5;
        AddFiniteTraveler(creature, speedUnitsPerHour: 10, [first], locationY);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new RouteCreatureToDestinationCommand
            {
                CreatureId = creature.Id,
                DestinationLocationId = locationZ,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 2,
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
        Assert.Equal(CreatureMovement.Walking, updatedCreature.Movement);
        Assert.Equal(locationY, firstStep.LocationId);
        Assert.Equal(second.Id, firstStep.ConnectorId);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(1) * 2, replacement.StartedAtGameTime);
    }

    [Fact]
    public async Task Handle_AppliesPreemptionRules_ToEveryCreatureInTheBatch()
    {
        var worldId = Guid.NewGuid();
        var locationX = Guid.NewGuid();
        var locationY = Guid.NewGuid();
        var outbound = AddConnector(worldId, locationX, locationY);
        var inbound = AddReverseConnector(outbound);
        var creatures = Enumerable
            .Range(0, 2)
            .Select(_ => Builders.MakeCreature(worldId, locationId: locationX))
            .ToArray();
        foreach (var creature in creatures)
        {
            creature.MovementSpeed = 20;
            AddFiniteTraveler(creature, speedUnitsPerHour: 10, [outbound], locationY);
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var decisionGameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 0.4;

        var results = await _batchHandler.Handle(
            new RouteCreaturesToDestinationsCommand
            {
                Routes = creatures
                    .Select(creature => new CreatureRouteRequest(
                        creature.Id,
                        locationX,
                        decisionGameTime,
                        "Returning to the gate."
                    ))
                    .ToArray(),
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var replacementIds = results
            .Values.Select(result => result.RouteTravelerId!.Value)
            .ToArray();
        var replacements = await _context
            .RouteTravelers.Where(traveler => replacementIds.AsEnumerable().Contains(traveler.Id))
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var routeId = Assert.Single(replacements.Select(traveler => traveler.RouteId).Distinct());
        var steps = await _context
            .RouteSteps.Where(step => step.RouteId == routeId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results.Values, result => Assert.False(result.IsAlreadyAtDestination));
        Assert.All(
            replacements,
            traveler =>
            {
                Assert.Equal(GameClock.Epoch, traveler.StartedAtGameTime);
                Assert.Equal(10, traveler.SpeedUnitsPerHour);
            }
        );
        Assert.Equal([outbound.Id, inbound.Id, null], steps.Select(step => step.ConnectorId));
    }

    private LocationConnector AddConnector(
        Guid worldId,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var connector = Builders.MakeLocationConnector(
            originLocationId,
            destinationLocationId,
            worldId
        );
        _context.LocationConnectors.Add(connector);
        _context.TravelNodes.AddRange(
            Builders.MakeExitNode(connector),
            Builders.MakeArrivalNode(connector)
        );
        return connector;
    }

    private LocationConnector AddReverseConnector(LocationConnector forward)
    {
        var reverse = Builders.MakeLocationConnector(
            forward.DestinationLocationId,
            forward.OriginLocationId,
            forward.WorldId
        );
        reverse.OriginNodeId = forward.DestinationNodeId;
        reverse.DestinationNodeId = forward.OriginNodeId;
        _context.LocationConnectors.Add(reverse);
        return reverse;
    }

    private RouteTraveler AddFiniteTraveler(
        Creature creature,
        double speedUnitsPerHour,
        IReadOnlyList<LocationConnector> connectors,
        Guid destinationLocationId,
        double legDistance = 10
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
            StartedAtGameTime = GameClock.Epoch,
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
                        Distance = legDistance,
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

    private async Task<ResolvedRouteTravelerPosition> Resolve(Guid travelerId, GameInstant gameTime)
    {
        var handler = _services.GetRequiredService<ResolveRouteTravelerPositionsQueryHandler>();
        var positions = await handler.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = [travelerId],
                GameTime = gameTime,
            },
            TestContext.Current.CancellationToken
        );
        return positions[travelerId];
    }
}
