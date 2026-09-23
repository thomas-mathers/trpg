using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public record ResolvedRouteTravelerPosition(RoutePosition Position, Guid NextLocationId);

public class ResolveRouteTravelerPositionsQuery
{
    public required IReadOnlyCollection<Guid> RouteTravelerIds { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required float SpeedUnitsPerHour { get; init; }
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
            .Where(t => query.RouteTravelerIds.AsEnumerable().Contains(t.Id))
            .ToArrayAsync(cancellationToken);
        var routeIds = travelers.Select(traveler => traveler.RouteId).Distinct().ToArray();

        var routesById = await context
            .Routes.AsNoTracking()
            .Where(route => routeIds.AsEnumerable().Contains(route.Id))
            .ToDictionaryAsync(route => route.Id, cancellationToken);

        var storedStops = await context
            .RouteStops.AsNoTracking()
            .Where(stop => routeIds.AsEnumerable().Contains(stop.RouteId))
            .OrderBy(stop => stop.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var stopsByRouteId = storedStops
            .GroupBy(stop => stop.RouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(stop => new RouteWaypoint(stop.LocationId, stop.DistanceToNextStop))
                        .ToArray()
            );

        return ResolvePositions(query, travelers, routesById, stopsByRouteId);
    }

    private static IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition> ResolvePositions(
        ResolveRouteTravelerPositionsQuery query,
        IReadOnlyCollection<RouteTraveler> travelers,
        IReadOnlyDictionary<Guid, Route> routesById,
        IReadOnlyDictionary<Guid, RouteWaypoint[]> stopsByRouteId
    )
    {
        var elapsedHours = query.Playtime / GameClock.RealTimePerInGameHour;
        return travelers.ToDictionary(
            traveler => traveler.Id,
            traveler =>
            {
                var route = routesById[traveler.RouteId];
                var stops = RouteCycle.ToTravelOrder(
                    stopsByRouteId[traveler.RouteId],
                    traveler.Direction
                );
                var position = RouteCycle.Resolve(
                    stops,
                    route.LingerHours,
                    query.SpeedUnitsPerHour,
                    elapsedHours + traveler.PhaseOffsetHours
                );
                var nextLocationId = position switch
                {
                    RoutePosition.Lingering lingering => stops[
                        (lingering.StopIndex + 1) % stops.Count
                    ].LocationId,
                    RoutePosition.InTransit inTransit => inTransit.ToLocationId,
                    _ => throw new InvalidOperationException("Unknown route position."),
                };
                return new ResolvedRouteTravelerPosition(position, nextLocationId);
            }
        );
    }
}
