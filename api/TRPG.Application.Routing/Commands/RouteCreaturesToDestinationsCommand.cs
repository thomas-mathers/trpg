using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public record CreatureRouteRequest(
    Guid CreatureId,
    Guid DestinationLocationId,
    TimeSpan Playtime,
    string Purpose
);

public class RouteCreaturesToDestinationsCommand
{
    public required IReadOnlyCollection<CreatureRouteRequest> Routes { get; init; }
}

public record RouteCreatureResult(
    Guid? RouteTravelerId,
    bool IsAlreadyAtDestination,
    TimeSpan ArrivesAtPlaytime
);

internal class RouteCreaturesToDestinationsCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
)
    : ICommandHandler<
        RouteCreaturesToDestinationsCommand,
        IReadOnlyDictionary<Guid, RouteCreatureResult>
    >
{
    public async Task<IReadOnlyDictionary<Guid, RouteCreatureResult>> Handle(
        RouteCreaturesToDestinationsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequests(command.Routes);
        if (command.Routes.Count == 0)
        {
            return new Dictionary<Guid, RouteCreatureResult>();
        }

        var creatures = await LoadCreatures(command.Routes, cancellationToken);
        ValidateCreatures(creatures.Values.ToArray());
        var worldId = creatures.Values.First().WorldId;
        var topology = await LoadTopology(worldId, cancellationToken);
        var activeRoutes = await LoadActiveRoutes(
            creatures.Keys.ToArray(),
            topology,
            cancellationToken
        );
        var plans = command.Routes.ToDictionary(
            request => request.CreatureId,
            request => BuildPlan(request, creatures[request.CreatureId], activeRoutes, topology)
        );

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await CreatureRouteCleaner.Remove(context, creatures.Keys.ToArray(), cancellationToken);
        var routeCache = await LoadRouteCache(worldId, cancellationToken);
        var results = CreateTravelers(worldId, plans, topology, routeCache);
        await PersistCreatureTargets(plans.Values.ToArray(), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();
        return results;
    }

    private async Task<IReadOnlyDictionary<Guid, Creature>> LoadCreatures(
        IReadOnlyCollection<CreatureRouteRequest> requests,
        CancellationToken cancellationToken
    )
    {
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = requests.Select(request => request.CreatureId).ToArray(),
            },
            cancellationToken
        );
        var missingId = requests
            .Select(request => request.CreatureId)
            .Where(id => !creatures.ContainsKey(id))
            .Select(id => (Guid?)id)
            .FirstOrDefault();
        return missingId == null
            ? creatures
            : throw new EntityNotFoundException(nameof(Creature), missingId.Value);
    }

    private async Task<RouteTopology> LoadTopology(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var connectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        var travelConnectors = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        return new RouteTopology(
            connectors,
            travelConnectors,
            travelConnectors.ToDictionary(connector => connector.ConnectorId)
        );
    }

    private async Task<IReadOnlyDictionary<Guid, ActiveCreatureRoute>> LoadActiveRoutes(
        IReadOnlyCollection<Guid> creatureIds,
        RouteTopology topology,
        CancellationToken cancellationToken
    )
    {
        var memberships = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => creatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        if (memberships.GroupBy(member => member.CreatureId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("A creature cannot have multiple active routes.");
        }

        var travelerIds = memberships.Select(member => member.RouteTravelerId).ToArray();
        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => travelerIds.AsEnumerable().Contains(traveler.Id))
            .ToDictionaryAsync(traveler => traveler.Id, cancellationToken);
        var routeIds = travelers.Values.Select(traveler => traveler.RouteId).Distinct().ToArray();

        var routes = await context
            .Routes.AsNoTracking()
            .Where(route => routeIds.AsEnumerable().Contains(route.Id))
            .ToDictionaryAsync(route => route.Id, cancellationToken);
        var stepsByRouteId = await LoadTimelineSteps(routeIds, topology, cancellationToken);

        return memberships.ToDictionary(
            member => member.CreatureId,
            member =>
            {
                var traveler = travelers[member.RouteTravelerId];
                return new ActiveCreatureRoute(
                    traveler,
                    routes[traveler.RouteId],
                    stepsByRouteId[traveler.RouteId]
                );
            }
        );
    }

    private async Task<
        IReadOnlyDictionary<Guid, IReadOnlyList<RouteTimelineStep>>
    > LoadTimelineSteps(
        IReadOnlyCollection<Guid> routeIds,
        RouteTopology topology,
        CancellationToken cancellationToken
    )
    {
        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        return steps
            .GroupBy(step => step.RouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<RouteTimelineStep>)
                        group
                            .Select(step => new RouteTimelineStep(
                                step.LocationId,
                                step.ConnectorId,
                                step.ConnectorId == null
                                    ? 0
                                    : topology.TravelByConnectorId[step.ConnectorId.Value].Distance,
                                step.DwellHours
                            ))
                            .ToArray()
            );
    }

    private static CreatureRoutePlan BuildPlan(
        CreatureRouteRequest request,
        Creature creature,
        IReadOnlyDictionary<Guid, ActiveCreatureRoute> activeRoutes,
        RouteTopology topology
    )
    {
        if (!activeRoutes.TryGetValue(creature.Id, out var activeRoute))
        {
            return BuildStationaryPlan(request, creature, creature.LocationId, topology);
        }

        var resolved = ResolveRouteTravelerPositionsQueryHandler.Resolve(
            activeRoute.Traveler,
            activeRoute.Route,
            activeRoute.Steps,
            request.Playtime
        );
        return resolved.Position switch
        {
            RouteTimelinePosition.InTransit inTransit => BuildInTransitPlan(
                request,
                creature,
                activeRoute,
                inTransit,
                topology
            ),
            RouteTimelinePosition.Arrived arrived => BuildStationaryPlan(
                request,
                creature,
                arrived.LocationId,
                topology
            ),
            RouteTimelinePosition.Pending pending => BuildValidatedStationaryPlan(
                request,
                creature,
                pending.LocationId,
                topology
            ),
            RouteTimelinePosition.Lingering lingering => BuildValidatedStationaryPlan(
                request,
                creature,
                lingering.LocationId,
                topology
            ),
            _ => throw new InvalidOperationException("Unknown route position."),
        };
    }

    private static CreatureRoutePlan BuildValidatedStationaryPlan(
        CreatureRouteRequest request,
        Creature creature,
        Guid routeLocationId,
        RouteTopology topology
    )
    {
        if (creature.LocationId != routeLocationId)
        {
            throw new InvalidOperationException(
                "A stationary route position must match the creature location."
            );
        }
        return BuildStationaryPlan(request, creature, creature.LocationId, topology);
    }

    private static CreatureRoutePlan BuildStationaryPlan(
        CreatureRouteRequest request,
        Creature creature,
        Guid originLocationId,
        RouteTopology topology
    )
    {
        if (originLocationId == request.DestinationLocationId)
        {
            return CreatureRoutePlan.AlreadyAt(request, creature, originLocationId);
        }

        var path = FindPath(topology, originLocationId, request.DestinationLocationId);
        EnsurePathExists(path);
        return new CreatureRoutePlan(
            request,
            creature,
            path,
            request.Playtime,
            creature.MovementSpeed,
            originLocationId,
            IsAlreadyAtDestination: false
        );
    }

    private static CreatureRoutePlan BuildInTransitPlan(
        CreatureRouteRequest request,
        Creature creature,
        ActiveCreatureRoute activeRoute,
        RouteTimelinePosition.InTransit inTransit,
        RouteTopology topology
    )
    {
        if (creature.LocationId != inTransit.FromLocationId)
        {
            throw new InvalidOperationException(
                "An in-transit route must be anchored to the creature's departure location."
            );
        }

        var tail = FindPath(topology, inTransit.ToLocationId, request.DestinationLocationId);
        if (inTransit.ToLocationId != request.DestinationLocationId)
        {
            EnsurePathExists(tail);
        }

        var currentLeg = new RoutePathLeg(
            inTransit.FromLocationId,
            inTransit.ConnectorId,
            inTransit.ToLocationId
        );
        var path = new[] { currentLeg }.Concat(tail).ToArray();
        var currentDistance = topology.TravelByConnectorId[inTransit.ConnectorId].Distance;
        var elapsedHours =
            currentDistance / activeRoute.Traveler.SpeedUnitsPerHour - inTransit.HoursUntilArrival;
        return new CreatureRoutePlan(
            request,
            creature,
            path,
            request.Playtime - GameClock.RealTimePerInGameHour * elapsedHours,
            activeRoute.Traveler.SpeedUnitsPerHour,
            inTransit.FromLocationId,
            IsAlreadyAtDestination: false
        );
    }

    private static IReadOnlyList<RoutePathLeg> FindPath(
        RouteTopology topology,
        Guid originLocationId,
        Guid destinationLocationId
    ) =>
        originLocationId == destinationLocationId
            ? []
            : RoutePathfinder.FindShortestPath(
                topology.Connectors,
                topology.TravelConnectors,
                originLocationId,
                destinationLocationId
            );

    private async Task<FiniteRouteCache> LoadRouteCache(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var routes = await context
            .Routes.Where(route =>
                route.WorldId == worldId && route.Traversal == RouteTraversal.Finite
            )
            .ToArrayAsync(cancellationToken);
        var routeIds = routes.Select(route => route.Id).ToArray();

        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        return new FiniteRouteCache(
            routes.ToList(),
            steps
                .GroupBy(step => step.RouteId)
                .ToDictionary(group => group.Key, group => group.ToArray())
        );
    }

    private IReadOnlyDictionary<Guid, RouteCreatureResult> CreateTravelers(
        Guid worldId,
        IReadOnlyDictionary<Guid, CreatureRoutePlan> plans,
        RouteTopology topology,
        FiniteRouteCache routeCache
    )
    {
        var results = new Dictionary<Guid, RouteCreatureResult>();
        foreach (var plan in plans.Values)
        {
            if (plan.IsAlreadyAtDestination)
            {
                results[plan.Creature.Id] = new RouteCreatureResult(
                    null,
                    IsAlreadyAtDestination: true,
                    plan.Request.Playtime
                );
                continue;
            }

            var route = GetOrCreateRoute(worldId, plan.Path, routeCache);
            var traveler = AddTraveler(worldId, plan, route.Id);
            var travelHours =
                plan.Path.Sum(leg => topology.TravelByConnectorId[leg.ConnectorId].Distance)
                / plan.SpeedUnitsPerHour;
            results[plan.Creature.Id] = new RouteCreatureResult(
                traveler.Id,
                IsAlreadyAtDestination: false,
                plan.StartedAtPlaytime + GameClock.RealTimePerInGameHour * travelHours
            );
        }
        return results;
    }

    private Route GetOrCreateRoute(
        Guid worldId,
        IReadOnlyList<RoutePathLeg> path,
        FiniteRouteCache routeCache
    )
    {
        var existing = routeCache.Routes.FirstOrDefault(route =>
            routeCache.StepsByRouteId.TryGetValue(route.Id, out var steps) && Matches(path, steps)
        );
        if (existing != null)
        {
            return existing;
        }

        var route = new Route
        {
            WorldId = worldId,
            Name = $"{path[0].OriginLocationId:N} to {path[^1].DestinationLocationId:N}",
            Traversal = RouteTraversal.Finite,
        };
        var steps = BuildSteps(worldId, route.Id, path);
        context.Routes.Add(route);
        context.RouteSteps.AddRange(steps);
        routeCache.Routes.Add(route);
        routeCache.StepsByRouteId[route.Id] = steps;
        return route;
    }

    private RouteTraveler AddTraveler(Guid worldId, CreatureRoutePlan plan, Guid routeId)
    {
        var traveler = new RouteTraveler
        {
            WorldId = worldId,
            RouteId = routeId,
            StartedAtPlaytime = plan.StartedAtPlaytime,
            SpeedUnitsPerHour = plan.SpeedUnitsPerHour,
            Purpose = plan.Request.Purpose,
        };
        context.RouteTravelers.Add(traveler);
        context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = worldId,
                RouteTravelerId = traveler.Id,
                CreatureId = plan.Creature.Id,
            }
        );
        return traveler;
    }

    private async Task PersistCreatureTargets(
        IReadOnlyCollection<CreatureRoutePlan> plans,
        CancellationToken cancellationToken
    )
    {
        var creatureIdsByTarget = plans
            .Select(plan => new
            {
                Target = new CreatureTarget(
                    plan.Creature.LocationId == plan.AnchorLocationId
                        ? null
                        : plan.AnchorLocationId,
                    plan.IsAlreadyAtDestination ? null : CreatureState.Walking
                ),
                plan.Creature.Id,
            })
            .Where(entry => entry.Target.LocationId != null || entry.Target.State != null)
            .GroupBy(entry => entry.Target)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Id).ToArray());
        foreach (var (target, creatureIds) in creatureIdsByTarget)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = creatureIds,
                    LocationId = target.LocationId,
                    State = target.State,
                },
                cancellationToken
            );
        }
    }

    private static void ValidateRequests(IReadOnlyCollection<CreatureRouteRequest> requests)
    {
        if (requests.Select(request => request.CreatureId).Distinct().Count() != requests.Count)
        {
            throw new ArgumentException(
                "A creature can only receive one route at a time.",
                nameof(requests)
            );
        }
    }

    private static void ValidateCreatures(IReadOnlyCollection<Creature> creatures)
    {
        if (creatures.Any(creature => creature.MovementSpeed <= 0))
        {
            throw new InvalidOperationException("A creature must have positive movement speed.");
        }
        if (creatures.Select(creature => creature.WorldId).Distinct().Count() != 1)
        {
            throw new InvalidOperationException("A routing batch must belong to one world.");
        }
    }

    private static void EnsurePathExists(IReadOnlyCollection<RoutePathLeg> path)
    {
        if (path.Count == 0)
        {
            throw new InvalidOperationException("No measured route connects the two locations.");
        }
    }

    private static bool Matches(IReadOnlyList<RoutePathLeg> path, IReadOnlyList<RouteStep> steps)
    {
        if (steps.Count != path.Count + 1)
        {
            return false;
        }

        return path.Select((leg, index) => (leg, index))
                .All(entry =>
                    steps[entry.index].LocationId == entry.leg.OriginLocationId
                    && steps[entry.index].ConnectorId == entry.leg.ConnectorId
                )
            && steps[^1].LocationId == path[^1].DestinationLocationId
            && steps[^1].ConnectorId == null;
    }

    private static RouteStep[] BuildSteps(
        Guid worldId,
        Guid routeId,
        IReadOnlyList<RoutePathLeg> path
    ) =>
        [
            .. path.Select(
                (leg, index) =>
                    new RouteStep
                    {
                        WorldId = worldId,
                        RouteId = routeId,
                        SequenceIndex = index,
                        LocationId = leg.OriginLocationId,
                        ConnectorId = leg.ConnectorId,
                        DwellHours = 0,
                    }
            ),
            new RouteStep
            {
                WorldId = worldId,
                RouteId = routeId,
                SequenceIndex = path.Count,
                LocationId = path[^1].DestinationLocationId,
                ConnectorId = null,
                DwellHours = 0,
            },
        ];

    private record RouteTopology(
        IReadOnlyCollection<LocationConnector> Connectors,
        IReadOnlyCollection<TravelConnector> TravelConnectors,
        IReadOnlyDictionary<Guid, TravelConnector> TravelByConnectorId
    );

    private record ActiveCreatureRoute(
        RouteTraveler Traveler,
        Route Route,
        IReadOnlyList<RouteTimelineStep> Steps
    );

    private record CreatureRoutePlan(
        CreatureRouteRequest Request,
        Creature Creature,
        IReadOnlyList<RoutePathLeg> Path,
        TimeSpan StartedAtPlaytime,
        double SpeedUnitsPerHour,
        Guid AnchorLocationId,
        bool IsAlreadyAtDestination
    )
    {
        public static CreatureRoutePlan AlreadyAt(
            CreatureRouteRequest request,
            Creature creature,
            Guid locationId
        ) =>
            new(
                request,
                creature,
                [],
                request.Playtime,
                creature.MovementSpeed,
                locationId,
                IsAlreadyAtDestination: true
            );
    }

    private record CreatureTarget(Guid? LocationId, CreatureState? State);

    private record FiniteRouteCache(
        List<Route> Routes,
        Dictionary<Guid, RouteStep[]> StepsByRouteId
    );
}
