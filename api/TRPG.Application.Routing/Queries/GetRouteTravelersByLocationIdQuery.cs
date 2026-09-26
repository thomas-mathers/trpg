using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public record RouteTravelerSummary(
    Guid RouteTravelerId,
    Guid RouteId,
    string RouteName,
    RouteTraversal Traversal,
    GameInstant StartedAtGameTime,
    double SpeedUnitsPerHour,
    IReadOnlyList<RouteTimelineStep> Steps
);

public class GetRouteTravelersByLocationIdQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetRouteTravelersByLocationIdQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteTravelersByLocationIdQuery, IReadOnlyList<RouteTravelerSummary>>
{
    public async Task<IReadOnlyList<RouteTravelerSummary>> Handle(
        GetRouteTravelersByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var routeIds = await context
            .RouteSteps.AsNoTracking()
            .Where(step => step.LocationId == query.LocationId)
            .Select(step => step.RouteId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (routeIds.Length == 0)
        {
            return [];
        }

        var routesById = await context
            .Routes.AsNoTracking()
            .Where(route =>
                route.WorldId == query.WorldId && routeIds.AsEnumerable().Contains(route.Id)
            )
            .ToDictionaryAsync(route => route.Id, cancellationToken);

        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => routeIds.AsEnumerable().Contains(traveler.RouteId))
            .ToArrayAsync(cancellationToken);

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
        var stepsByRouteId = routeSteps
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
                                    : distancesByConnectorId[step.ConnectorId.Value],
                                step.DwellHours
                            ))
                            .ToArray()
            );

        return travelers
            .Where(traveler => routesById.ContainsKey(traveler.RouteId))
            .Select(traveler =>
            {
                var route = routesById[traveler.RouteId];
                return new RouteTravelerSummary(
                    traveler.Id,
                    traveler.RouteId,
                    route.Name,
                    route.Traversal,
                    traveler.StartedAtGameTime,
                    traveler.SpeedUnitsPerHour,
                    stepsByRouteId[traveler.RouteId]
                );
            })
            .ToArray();
    }
}
