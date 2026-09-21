using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Caravans.Queries;

public class ResolveCaravanPositionQuery
{
    public required Guid CaravanId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class ResolveCaravanPositionQueryHandler(
    ICaravansDbContext context,
    IOptionsSnapshot<CaravanOptions> caravanOptions
) : IQueryHandler<ResolveCaravanPositionQuery, CaravanPosition?>
{
    public async Task<CaravanPosition?> Handle(
        ResolveCaravanPositionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var caravan = await context
            .Caravans.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.CaravanId, cancellationToken);
        if (caravan == null)
        {
            return null;
        }

        var route = await context
            .CaravanRoutes.AsNoTracking()
            .FirstAsync(r => r.Id == caravan.CaravanRouteId, cancellationToken);

        var storedStops = await context
            .CaravanRouteStops.AsNoTracking()
            .Where(s => s.CaravanRouteId == caravan.CaravanRouteId)
            .OrderBy(s => s.SequenceIndex)
            .Select(s => new CaravanStop(s.LocationId, s.DistanceToNextStop))
            .ToArrayAsync(cancellationToken);
        var stops = CaravanCycle.ToTravelOrder(storedStops, caravan.Direction);

        // Playtime is banked real-time hours, not in-game hours — GameClock runs the in-game clock
        // faster (GameClock.RealTimePerInGameHour), and CaravanCycle's stop/leg hours are all
        // in-game hours (matching how player travel time is computed), so this needs the same
        // conversion before comparing the two.
        var elapsedHours =
            query.Playtime / GameClock.RealTimePerInGameHour + caravan.PhaseOffsetHours;

        return CaravanCycle.Resolve(
            stops,
            route.LingerHours,
            caravanOptions.Value.SpeedUnitsPerHour,
            elapsedHours
        );
    }
}
