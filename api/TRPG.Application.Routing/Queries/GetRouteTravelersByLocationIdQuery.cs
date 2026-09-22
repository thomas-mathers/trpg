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
    double LingerHours,
    RouteDirection Direction,
    IReadOnlyList<RouteWaypoint> Stops
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
            .RouteStops.AsNoTracking()
            .Where(s => s.LocationId == query.LocationId)
            .Select(s => s.RouteId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (routeIds.Length == 0)
        {
            return [];
        }

        var routesById = await context
            .Routes.AsNoTracking()
            .Where(r => r.WorldId == query.WorldId && routeIds.AsEnumerable().Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(t => routeIds.AsEnumerable().Contains(t.RouteId))
            .ToArrayAsync(cancellationToken);

        var stopsByRouteId = await context
            .RouteStops.AsNoTracking()
            .Where(s => routeIds.AsEnumerable().Contains(s.RouteId))
            .OrderBy(s => s.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var stopsGroupedByRouteId = stopsByRouteId
            .GroupBy(s => s.RouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<RouteWaypoint>)
                        group
                            .Select(s => new RouteWaypoint(s.LocationId, s.DistanceToNextStop))
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
                    route.LingerHours,
                    traveler.Direction,
                    RouteCycle.ToTravelOrder(
                        stopsGroupedByRouteId[traveler.RouteId],
                        traveler.Direction
                    )
                );
            })
            .ToArray();
    }
}
