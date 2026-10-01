using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyAnyAvailableSeatCommand
{
    public required Guid LocationId { get; init; }
    public required Guid CreatureId { get; init; }
    public Guid? PreferredSeatId { get; init; }
}

// Claims one free seat atomically: finds a candidate then conditionally updates it, retrying with
// that candidate excluded if another claim won the race, instead of fetching every seat up front.
// Returns the claimed seat's pose, or null when no seat was available.
internal class TryOccupyAnyAvailableSeatCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyAnyAvailableSeatCommand, Placement?>
{
    public async Task<Placement?> Handle(
        TryOccupyAnyAvailableSeatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // Reclaiming the same seat the creature just vacated keeps a scarce-seat location's
        // occupants stable across resyncs instead of reshuffling to whichever creature is
        // processed first that tick.
        if (command.PreferredSeatId != null)
        {
            var reclaimed = await context
                .Props.OfType<Seat>()
                .Where(seat =>
                    seat.Id == command.PreferredSeatId
                    && seat.LocationId == command.LocationId
                    && seat.OccupantId == null
                )
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(seat => seat.OccupantId, command.CreatureId),
                    cancellationToken
                );
            if (reclaimed == 1)
            {
                return await GetPlacement(command.PreferredSeatId.Value, cancellationToken);
            }
        }

        var excludedSeatIds = new HashSet<Guid>();
        while (true)
        {
            var candidateSeatId = await context
                .Props.OfType<Seat>()
                .Where(seat =>
                    seat.LocationId == command.LocationId
                    && seat.OccupantId == null
                    && !excludedSeatIds.AsEnumerable().Contains(seat.Id)
                )
                .OrderBy(seat => seat.Id)
                .Select(seat => (Guid?)seat.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (candidateSeatId == null)
            {
                return null;
            }

            var updated = await context
                .Props.OfType<Seat>()
                .Where(seat => seat.Id == candidateSeatId && seat.OccupantId == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(seat => seat.OccupantId, command.CreatureId),
                    cancellationToken
                );
            if (updated == 1)
            {
                return await GetPlacement(candidateSeatId.Value, cancellationToken);
            }

            excludedSeatIds.Add(candidateSeatId.Value);
        }
    }

    private async Task<Placement> GetPlacement(Guid seatId, CancellationToken cancellationToken) =>
        await context
            .Props.Where(seat => seat.Id == seatId)
            .Select(seat => new Placement(seat.X, seat.Y, seat.Angle))
            .SingleAsync(cancellationToken);
}
