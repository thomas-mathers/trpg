using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Routing.Queries;

public class ResolveRouteTravelerPositionQuery
{
    public required Guid RouteTravelerId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required float SpeedUnitsPerHour { get; init; }
}

internal class ResolveRouteTravelerPositionQueryHandler(IRoutingDbContext context)
    : IQueryHandler<ResolveRouteTravelerPositionQuery, RoutePosition?>
{
    public async Task<RoutePosition?> Handle(
        ResolveRouteTravelerPositionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var traveler = await context
            .RouteTravelers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.RouteTravelerId, cancellationToken);
        if (traveler == null)
        {
            return null;
        }

        var route = await context
            .Routes.AsNoTracking()
            .FirstAsync(r => r.Id == traveler.RouteId, cancellationToken);

        var storedStops = await context
            .RouteStops.AsNoTracking()
            .Where(s => s.RouteId == traveler.RouteId)
            .OrderBy(s => s.SequenceIndex)
            .Select(s => new RouteWaypoint(s.LocationId, s.DistanceToNextStop))
            .ToArrayAsync(cancellationToken);
        var stops = RouteCycle.ToTravelOrder(storedStops, traveler.Direction);

        // Playtime is banked real-time hours, not in-game hours — GameClock runs the in-game clock
        // faster (GameClock.RealTimePerInGameHour), and RouteCycle's stop/leg hours are all
        // in-game hours (matching how player travel time is computed), so this needs the same
        // conversion before comparing the two.
        var elapsedHours =
            query.Playtime / GameClock.RealTimePerInGameHour + traveler.PhaseOffsetHours;

        return RouteCycle.Resolve(stops, route.LingerHours, query.SpeedUnitsPerHour, elapsedHours);
    }
}
