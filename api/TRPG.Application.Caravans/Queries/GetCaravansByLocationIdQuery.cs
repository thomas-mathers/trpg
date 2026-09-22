using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public record CaravanSummary(
    Guid CaravanId,
    Guid CaravanRouteId,
    string RouteName,
    int TicketFeeGold,
    double LingerHours,
    CaravanDirection Direction,
    IReadOnlyList<CaravanStop> Stops
);

public class GetCaravansByLocationIdQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetCaravansByLocationIdQueryHandler(ICaravansDbContext context)
    : IQueryHandler<GetCaravansByLocationIdQuery, IReadOnlyList<CaravanSummary>>
{
    public async Task<IReadOnlyList<CaravanSummary>> Handle(
        GetCaravansByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var routeIds = await context
            .CaravanRouteStops.AsNoTracking()
            .Where(s => s.LocationId == query.LocationId)
            .Select(s => s.CaravanRouteId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (routeIds.Length == 0)
        {
            return [];
        }

        var routesById = await context
            .CaravanRoutes.AsNoTracking()
            .Where(r => r.WorldId == query.WorldId && routeIds.AsEnumerable().Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var caravans = await context
            .Caravans.AsNoTracking()
            .Where(c => routeIds.AsEnumerable().Contains(c.CaravanRouteId))
            .ToArrayAsync(cancellationToken);

        var stopsByRouteId = await context
            .CaravanRouteStops.AsNoTracking()
            .Where(s => routeIds.AsEnumerable().Contains(s.CaravanRouteId))
            .OrderBy(s => s.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var stopsGroupedByRouteId = stopsByRouteId
            .GroupBy(s => s.CaravanRouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<CaravanStop>)
                        group
                            .Select(s => new CaravanStop(s.LocationId, s.DistanceToNextStop))
                            .ToArray()
            );

        return caravans
            .Where(caravan => routesById.ContainsKey(caravan.CaravanRouteId))
            .Select(caravan =>
            {
                var route = routesById[caravan.CaravanRouteId];
                return new CaravanSummary(
                    caravan.Id,
                    caravan.CaravanRouteId,
                    route.Name,
                    route.TicketFeeGold,
                    route.LingerHours,
                    caravan.Direction,
                    CaravanCycle.ToTravelOrder(
                        stopsGroupedByRouteId[caravan.CaravanRouteId],
                        caravan.Direction
                    )
                );
            })
            .ToArray();
    }
}
