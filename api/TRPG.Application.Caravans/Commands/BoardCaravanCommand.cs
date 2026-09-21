using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Caravans.Commands;

public class BoardCaravanCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid CaravanId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

public enum BoardCaravanOutcome
{
    Boarded,
    NoTicket,
    CaravanNotPresent,
}

public record BoardCaravanResult(
    BoardCaravanOutcome Outcome,
    Guid? DestinationLocationId = null,
    double? TravelTimeHours = null
);

internal class BoardCaravanCommandHandler(
    ICaravansDbContext context,
    IQueryHandler<ResolveCaravanPositionQuery, CaravanPosition?> resolveCaravanPosition,
    IOptionsSnapshot<CaravanOptions> caravanOptions
) : ICommandHandler<BoardCaravanCommand, BoardCaravanResult>
{
    public async Task<BoardCaravanResult> Handle(
        BoardCaravanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = await context.CaravanTickets.FirstOrDefaultAsync(
            t => t.CreatureId == command.PlayerId && t.CaravanId == command.CaravanId,
            cancellationToken
        );
        if (ticket == null)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.NoTicket);
        }

        // Once ticketed, the caravan holds for the player as long as they haven't left the stop —
        // re-deriving live position here would let ordinary narration-time overhead (every
        // narrated turn, including the purchase itself, advances playtime a little) make an
        // already-valid ticket look like the caravan already departed.
        if (ticket.OriginStopLocationId != command.PlayerLocationId)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.CaravanNotPresent);
        }

        var positionAtPurchase = await resolveCaravanPosition.Handle(
            new ResolveCaravanPositionQuery
            {
                CaravanId = command.CaravanId,
                Playtime = ticket.PurchasedAtPlaytime,
            },
            cancellationToken
        );
        if (positionAtPurchase is not CaravanPosition.Lingering lingeringAtPurchase)
        {
            return new BoardCaravanResult(BoardCaravanOutcome.CaravanNotPresent);
        }

        var caravan = await context
            .Caravans.AsNoTracking()
            .FirstAsync(c => c.Id == command.CaravanId, cancellationToken);
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

        var fromIndex = stops.ToList().FindIndex(s => s.LocationId == ticket.OriginStopLocationId);
        var toIndex = stops.ToList().FindIndex(s => s.LocationId == ticket.DestinationLocationId);

        // The trip's total duration is fixed the moment the ticket is valid to buy — anchoring to
        // the purchase instant (rather than "now") keeps the destination arrival in sync with the
        // caravan's own schedule no matter how much narration-time overhead accrues between
        // buying the ticket and actually clicking Board.
        var idealTripHours =
            lingeringAtPurchase.HoursUntilDeparture
            + CaravanCycle.HoursBetween(
                stops,
                route.LingerHours,
                caravanOptions.Value.SpeedUnitsPerHour,
                fromIndex,
                toIndex
            );
        var elapsedHoursSincePurchase =
            (command.Playtime - ticket.PurchasedAtPlaytime) / GameClock.RealTimePerInGameHour;
        var travelTimeHours = Math.Max(0, idealTripHours - elapsedHoursSincePurchase);

        var destinationLocationId = ticket.DestinationLocationId;
        context.CaravanTickets.Remove(ticket);
        await context.SaveChangesAsync(cancellationToken);

        return new BoardCaravanResult(
            BoardCaravanOutcome.Boarded,
            destinationLocationId,
            travelTimeHours
        );
    }
}
