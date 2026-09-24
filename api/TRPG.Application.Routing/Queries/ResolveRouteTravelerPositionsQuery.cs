using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public record ResolvedRouteTravelerPosition(RouteTimelinePosition Position, Guid NextLocationId);

public class ResolveRouteTravelerPositionsQuery
{
    public required IReadOnlyCollection<Guid> RouteTravelerIds { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class ResolveRouteTravelerPositionsQueryHandler(IRoutingDbContext context)
    : IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    >
{
    public async Task<IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>> Handle(
        ResolveRouteTravelerPositionsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => query.RouteTravelerIds.AsEnumerable().Contains(traveler.Id))
            .ToArrayAsync(cancellationToken);
        var routeIds = travelers.Select(traveler => traveler.RouteId).Distinct().ToArray();

        var routesById = await context
            .Routes.AsNoTracking()
            .Where(route => routeIds.AsEnumerable().Contains(route.Id))
            .ToDictionaryAsync(route => route.Id, cancellationToken);

        var routeSteps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var connectorIds = routeSteps
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
        var timelineStepsByRouteId = routeSteps
            .GroupBy(step => step.RouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(step => new RouteTimelineStep(
                            step.LocationId,
                            step.ConnectorId,
                            step.ConnectorId == null
                                ? 0
                                : distancesByConnectorId[step.ConnectorId.Value],
                            step.DwellHours
                        ))
                        .ToArray()
            );

        return travelers.ToDictionary(
            traveler => traveler.Id,
            traveler =>
                Resolve(
                    traveler,
                    routesById[traveler.RouteId],
                    timelineStepsByRouteId[traveler.RouteId],
                    query.Playtime
                )
        );
    }

    internal static ResolvedRouteTravelerPosition Resolve(
        RouteTraveler traveler,
        Route route,
        IReadOnlyList<RouteTimelineStep> steps,
        TimeSpan playtime
    )
    {
        var position = RouteTimeline.Resolve(
            steps,
            route.Traversal,
            traveler.SpeedUnitsPerHour,
            traveler.StartedAtPlaytime,
            playtime
        );
        var nextLocationId = position switch
        {
            RouteTimelinePosition.Pending pending => pending.LocationId,
            RouteTimelinePosition.Lingering lingering => steps[
                (lingering.StepIndex + 1) % steps.Count
            ].LocationId,
            RouteTimelinePosition.InTransit inTransit => inTransit.ToLocationId,
            RouteTimelinePosition.Arrived arrived => arrived.LocationId,
            _ => throw new InvalidOperationException("Unknown route position."),
        };
        return new ResolvedRouteTravelerPosition(position, nextLocationId);
    }
}
