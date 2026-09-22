using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public record NextCaravanArrival(RouteDirection Direction, double HoursUntilArrival);

// Powers a caravan schedule sign's live "next arrival" text — unlike the seeder, which only ever
// runs once at world creation, this is recomputed from the current playtime on every read, so it
// never goes stale the way a value baked in at creation time would.
public class GetNextCaravanArrivalsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class GetNextCaravanArrivalsQueryHandler(
    IQueryHandler<
        GetRouteTravelersByLocationIdQuery,
        IReadOnlyList<RouteTravelerSummary>
    > getRouteTravelersByLocationId,
    ICaravansDbContext context,
    IOptionsSnapshot<CaravanOptions> caravanOptions
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

        var elapsedHours = query.Playtime / GameClock.RealTimePerInGameHour;

        // Several instances of the same direction can serve this stop — the player only cares
        // about whichever one gets here first, not every instance's own individual schedule.
        return caravanTravelers
            .Select(traveler =>
            {
                var stopIndex = traveler
                    .Stops.ToList()
                    .FindIndex(stop => stop.LocationId == query.LocationId);
                var hoursUntilArrival = RouteCycle.HoursUntilNextArrivalAt(
                    traveler.Stops,
                    traveler.LingerHours,
                    caravanOptions.Value.SpeedUnitsPerHour,
                    elapsedHours,
                    stopIndex
                );
                return (traveler.Direction, HoursUntilArrival: hoursUntilArrival);
            })
            .GroupBy(arrival => arrival.Direction)
            .Select(group => new NextCaravanArrival(
                group.Key,
                group.Min(arrival => arrival.HoursUntilArrival)
            ))
            .OrderBy(arrival => arrival.Direction)
            .ToArray();
    }
}
