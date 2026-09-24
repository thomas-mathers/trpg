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

public class RouteCreatureToDestinationCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid DestinationLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required string Purpose { get; init; }
}

public record RouteCreatureToDestinationResult(Guid? RouteTravelerId, bool IsAlreadyAtDestination);

internal class RouteCreatureToDestinationCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<RouteCreatureToDestinationCommand, RouteCreatureToDestinationResult>
{
    public async Task<RouteCreatureToDestinationResult> Handle(
        RouteCreatureToDestinationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await GetCreature(command.CreatureId, cancellationToken);
        var activeRoute = await GetActiveRoute(command.CreatureId, cancellationToken);
        var plan = await BuildPlan(command, creature, activeRoute, cancellationToken);

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await CreatureRouteCleaner.Remove(context, [command.CreatureId], cancellationToken);

        if (plan.IsAlreadyAtDestination)
        {
            await MaterializeCreature(creature, plan.AnchorLocationId, null, cancellationToken);
            transaction.Complete();
            return new RouteCreatureToDestinationResult(null, IsAlreadyAtDestination: true);
        }

        var route = await GetOrCreateRoute(creature.WorldId, plan.Path, cancellationToken);
        var traveler = new RouteTraveler
        {
            WorldId = creature.WorldId,
            RouteId = route.Id,
            StartedAtPlaytime = plan.StartedAtPlaytime,
            SpeedUnitsPerHour = plan.SpeedUnitsPerHour,
            Purpose = command.Purpose,
        };
        context.RouteTravelers.Add(traveler);
        context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = creature.WorldId,
                RouteTravelerId = traveler.Id,
                CreatureId = creature.Id,
            }
        );
        await MaterializeCreature(
            creature,
            plan.AnchorLocationId,
            CreatureState.Walking,
            cancellationToken
        );
        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();

        return new RouteCreatureToDestinationResult(traveler.Id, IsAlreadyAtDestination: false);
    }

    private async Task<Creature> GetCreature(Guid creatureId, CancellationToken cancellationToken)
    {
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = [creatureId] },
            cancellationToken
        );
        return creatures.GetValueOrDefault(creatureId)
            ?? throw new EntityNotFoundException(nameof(Creature), creatureId);
    }

    private async Task<ActiveCreatureRoute?> GetActiveRoute(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var travelerId = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => member.CreatureId == creatureId)
            .Select(member => (Guid?)member.RouteTravelerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (travelerId == null)
        {
            return null;
        }

        var traveler = await context
            .RouteTravelers.AsNoTracking()
            .SingleAsync(entry => entry.Id == travelerId.Value, cancellationToken);
        var route = await context
            .Routes.AsNoTracking()
            .SingleAsync(entry => entry.Id == traveler.RouteId, cancellationToken);
        var steps = await LoadTimelineSteps(route.Id, cancellationToken);
        return new ActiveCreatureRoute(traveler, route, steps);
    }

    private async Task<IReadOnlyList<RouteTimelineStep>> LoadTimelineSteps(
        Guid routeId,
        CancellationToken cancellationToken
    )
    {
        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => step.RouteId == routeId)
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var connectorIds = steps
            .Where(step => step.ConnectorId != null)
            .Select(step => step.ConnectorId!.Value)
            .Distinct()
            .ToArray();

        var distancesByConnectorId = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connectorIds.AsEnumerable().Contains(connector.ConnectorId))
            .ToDictionaryAsync(
                connector => connector.ConnectorId,
                connector => (double)connector.Distance,
                cancellationToken
            );
        return steps
            .Select(step => new RouteTimelineStep(
                step.LocationId,
                step.ConnectorId,
                step.ConnectorId == null ? 0 : distancesByConnectorId[step.ConnectorId.Value],
                step.DwellHours
            ))
            .ToArray();
    }

    private async Task<CreatureRoutePlan> BuildPlan(
        RouteCreatureToDestinationCommand command,
        Creature creature,
        ActiveCreatureRoute? activeRoute,
        CancellationToken cancellationToken
    )
    {
        if (creature.MovementSpeed <= 0)
        {
            throw new InvalidOperationException("A creature must have positive movement speed.");
        }

        if (activeRoute == null)
        {
            return await BuildStationaryPlan(
                creature,
                creature.LocationId,
                command,
                cancellationToken
            );
        }

        var resolved = ResolveRouteTravelerPositionsQueryHandler.Resolve(
            activeRoute.Traveler,
            activeRoute.Route,
            activeRoute.Steps,
            command.Playtime
        );
        return resolved.Position switch
        {
            RouteTimelinePosition.InTransit inTransit => await BuildInTransitPlan(
                creature,
                command,
                activeRoute,
                inTransit,
                cancellationToken
            ),
            RouteTimelinePosition.Arrived arrived => await BuildStationaryPlan(
                creature,
                arrived.LocationId,
                command,
                cancellationToken
            ),
            RouteTimelinePosition.Pending pending => await BuildValidatedStationaryPlan(
                creature,
                pending.LocationId,
                command,
                cancellationToken
            ),
            RouteTimelinePosition.Lingering lingering => await BuildValidatedStationaryPlan(
                creature,
                lingering.LocationId,
                command,
                cancellationToken
            ),
            _ => throw new InvalidOperationException("Unknown route position."),
        };
    }

    private async Task<CreatureRoutePlan> BuildValidatedStationaryPlan(
        Creature creature,
        Guid routeLocationId,
        RouteCreatureToDestinationCommand command,
        CancellationToken cancellationToken
    )
    {
        if (creature.LocationId != routeLocationId)
        {
            throw new InvalidOperationException(
                "A stationary route position must match the creature location."
            );
        }
        return await BuildStationaryPlan(creature, creature.LocationId, command, cancellationToken);
    }

    private async Task<CreatureRoutePlan> BuildStationaryPlan(
        Creature creature,
        Guid originLocationId,
        RouteCreatureToDestinationCommand command,
        CancellationToken cancellationToken
    )
    {
        if (originLocationId == command.DestinationLocationId)
        {
            return CreatureRoutePlan.AlreadyAt(
                originLocationId,
                creature.MovementSpeed,
                command.Playtime
            );
        }

        var path = await FindPath(
            creature.WorldId,
            originLocationId,
            command.DestinationLocationId,
            cancellationToken
        );
        EnsurePathExists(path);
        return new CreatureRoutePlan(
            path,
            command.Playtime,
            creature.MovementSpeed,
            originLocationId,
            IsAlreadyAtDestination: false
        );
    }

    private async Task<CreatureRoutePlan> BuildInTransitPlan(
        Creature creature,
        RouteCreatureToDestinationCommand command,
        ActiveCreatureRoute activeRoute,
        RouteTimelinePosition.InTransit inTransit,
        CancellationToken cancellationToken
    )
    {
        if (creature.LocationId != inTransit.FromLocationId)
        {
            throw new InvalidOperationException(
                "An in-transit route must be anchored to the creature's departure location."
            );
        }

        var tail = await FindPath(
            creature.WorldId,
            inTransit.ToLocationId,
            command.DestinationLocationId,
            cancellationToken
        );
        if (inTransit.ToLocationId != command.DestinationLocationId)
        {
            EnsurePathExists(tail);
        }

        var currentLeg = new RoutePathLeg(
            inTransit.FromLocationId,
            inTransit.ConnectorId,
            inTransit.ToLocationId
        );
        var path = new[] { currentLeg }.Concat(tail).ToArray();
        var currentDistance = activeRoute
            .Steps.First(step => step.ConnectorId == inTransit.ConnectorId)
            .Distance;
        var elapsedHours =
            currentDistance / activeRoute.Traveler.SpeedUnitsPerHour - inTransit.HoursUntilArrival;
        return new CreatureRoutePlan(
            path,
            command.Playtime - GameClock.RealTimePerInGameHour * elapsedHours,
            activeRoute.Traveler.SpeedUnitsPerHour,
            inTransit.FromLocationId,
            IsAlreadyAtDestination: false
        );
    }

    private async Task<IReadOnlyList<RoutePathLeg>> FindPath(
        Guid worldId,
        Guid originLocationId,
        Guid destinationLocationId,
        CancellationToken cancellationToken
    )
    {
        if (originLocationId == destinationLocationId)
        {
            return [];
        }

        var connectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        var travelConnectors = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        return RoutePathfinder.FindShortestPath(
            connectors,
            travelConnectors,
            originLocationId,
            destinationLocationId
        );
    }

    private static void EnsurePathExists(IReadOnlyCollection<RoutePathLeg> path)
    {
        if (path.Count == 0)
        {
            throw new InvalidOperationException("No measured route connects the two locations.");
        }
    }

    private async Task<Route> GetOrCreateRoute(
        Guid worldId,
        IReadOnlyList<RoutePathLeg> path,
        CancellationToken cancellationToken
    )
    {
        var finiteRoutes = await context
            .Routes.Where(route =>
                route.WorldId == worldId && route.Traversal == RouteTraversal.Finite
            )
            .ToArrayAsync(cancellationToken);
        var finiteRouteIds = finiteRoutes.Select(route => route.Id).ToArray();

        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => finiteRouteIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var stepsByRouteId = steps.GroupBy(step => step.RouteId).ToDictionary(group => group.Key);
        var existing = finiteRoutes.FirstOrDefault(route =>
            stepsByRouteId.TryGetValue(route.Id, out var routeSteps)
            && Matches(path, routeSteps.ToArray())
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
        context.Routes.Add(route);
        context.RouteSteps.AddRange(BuildSteps(worldId, route.Id, path));
        return route;
    }

    private async Task MaterializeCreature(
        Creature creature,
        Guid locationId,
        CreatureState? state,
        CancellationToken cancellationToken
    )
    {
        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [creature.Id],
                LocationId = creature.LocationId == locationId ? null : locationId,
                State = state,
            },
            cancellationToken
        );
    }

    private static bool Matches(IReadOnlyList<RoutePathLeg> path, IReadOnlyList<RouteStep> steps)
    {
        if (steps.Count != path.Count + 1)
        {
            return false;
        }

        for (var index = 0; index < path.Count; index++)
        {
            if (
                steps[index].LocationId != path[index].OriginLocationId
                || steps[index].ConnectorId != path[index].ConnectorId
            )
            {
                return false;
            }
        }

        return steps[^1].LocationId == path[^1].DestinationLocationId
            && steps[^1].ConnectorId == null;
    }

    private static IReadOnlyList<RouteStep> BuildSteps(
        Guid worldId,
        Guid routeId,
        IReadOnlyList<RoutePathLeg> path
    )
    {
        var steps = path.Select(
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
            )
            .ToList();
        steps.Add(
            new RouteStep
            {
                WorldId = worldId,
                RouteId = routeId,
                SequenceIndex = path.Count,
                LocationId = path[^1].DestinationLocationId,
                ConnectorId = null,
                DwellHours = 0,
            }
        );
        return steps;
    }

    private record ActiveCreatureRoute(
        RouteTraveler Traveler,
        Route Route,
        IReadOnlyList<RouteTimelineStep> Steps
    );

    private record CreatureRoutePlan(
        IReadOnlyList<RoutePathLeg> Path,
        TimeSpan StartedAtPlaytime,
        double SpeedUnitsPerHour,
        Guid AnchorLocationId,
        bool IsAlreadyAtDestination
    )
    {
        public static CreatureRoutePlan AlreadyAt(
            Guid locationId,
            double speedUnitsPerHour,
            TimeSpan playtime
        ) => new([], playtime, speedUnitsPerHour, locationId, IsAlreadyAtDestination: true);
    }
}
