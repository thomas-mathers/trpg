using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public record NextCaravanArrival(string RouteName, double HoursUntilArrival);

// Powers a caravan schedule sign's live "next arrival" text — unlike the seeder, which only ever
// runs once at world creation, this is recomputed from the current gameTime on every read, so it
// never goes stale the way a value baked in at creation time would.
public class GetNextCaravanArrivalsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class GetNextCaravanArrivalsQueryHandler(
    IQueryHandler<
        GetRouteTravelersByLocationIdQuery,
        IReadOnlyList<RouteTravelerSummary>
    > getRouteTravelersByLocationId,
    ICaravansDbContext context
) : IQueryHandler<GetNextCaravanArrivalsQuery, IReadOnlyList<NextCaravanArrival>>
{
    public async Task<IReadOnlyList<NextCaravanArrival>> Handle(
        GetNextCaravanArrivalsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var travelers = await getRouteTravelersByLocationId.Handle(
            new GetRouteTravelersByLocationIdQuery
            {
                WorldId = query.WorldId,
                LocationId = query.LocationId,
            },
            cancellationToken
        );

        var routeIds = travelers.Select(t => t.RouteId).Distinct().ToArray();
        var caravanRouteIds = await context
            .CaravanFares.AsNoTracking()
            .Where(f => routeIds.AsEnumerable().Contains(f.RouteId))
            .Select(f => f.RouteId)
            .ToArrayAsync(cancellationToken);
        var caravanTravelers = travelers.Where(t => caravanRouteIds.Contains(t.RouteId)).ToArray();

        return caravanTravelers
            .Select(traveler =>
            {
                var stopIndex = traveler
                    .Steps.ToList()
                    .FindIndex(step => step.LocationId == query.LocationId && step.DwellHours > 0);
                var hoursUntilArrival = RouteTimeline.HoursUntilNextArrivalAt(
                    traveler.Steps,
                    traveler.SpeedUnitsPerHour,
                    traveler.StartedAtGameTime,
                    query.GameTime,
                    stopIndex
                );
                return (traveler.RouteName, HoursUntilArrival: hoursUntilArrival);
            })
            .GroupBy(arrival => arrival.RouteName)
            .Select(group => new NextCaravanArrival(
                group.Key,
                group.Min(arrival => arrival.HoursUntilArrival)
            ))
            .OrderBy(arrival => arrival.RouteName)
            .ToArray();
    }
}
