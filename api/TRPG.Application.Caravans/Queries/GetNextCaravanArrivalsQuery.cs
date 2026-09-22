using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public record NextCaravanArrival(CaravanDirection Direction, double HoursUntilArrival);

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
        GetCaravansByLocationIdQuery,
        IReadOnlyList<CaravanSummary>
    > getCaravansByLocationId,
    IOptionsSnapshot<CaravanOptions> caravanOptions
) : IQueryHandler<GetNextCaravanArrivalsQuery, IReadOnlyList<NextCaravanArrival>>
{
    public async Task<IReadOnlyList<NextCaravanArrival>> Handle(
        GetNextCaravanArrivalsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var caravans = await getCaravansByLocationId.Handle(
            new GetCaravansByLocationIdQuery
            {
                WorldId = query.WorldId,
                LocationId = query.LocationId,
            },
            cancellationToken
        );
        var elapsedHours = query.Playtime / GameClock.RealTimePerInGameHour;

        // Several instances of the same direction can serve this stop — the player only cares
        // about whichever one gets here first, not every instance's own individual schedule.
        return caravans
            .Select(caravan =>
            {
                var stopIndex = caravan
                    .Stops.ToList()
                    .FindIndex(stop => stop.LocationId == query.LocationId);
                var hoursUntilArrival = CaravanCycle.HoursUntilNextArrivalAt(
                    caravan.Stops,
                    caravan.LingerHours,
                    caravanOptions.Value.SpeedUnitsPerHour,
                    elapsedHours,
                    stopIndex
                );
                return (caravan.Direction, HoursUntilArrival: hoursUntilArrival);
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
