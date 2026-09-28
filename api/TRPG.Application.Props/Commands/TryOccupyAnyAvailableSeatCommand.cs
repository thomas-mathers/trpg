using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyAnyAvailableSeatCommand
{
    public required Guid LocationId { get; init; }
    public required Guid CreatureId { get; init; }
}

// Claims one free seat atomically: finds a candidate then conditionally updates it, retrying with
// that candidate excluded if another claim won the race, instead of fetching every seat up front.
internal class TryOccupyAnyAvailableSeatCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyAnyAvailableSeatCommand, bool>
{
    public async Task<bool> Handle(
        TryOccupyAnyAvailableSeatCommand command,
        CancellationToken cancellationToken = default
    )
    {
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
                return false;
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
                return true;
            }

            excludedSeatIds.Add(candidateSeatId.Value);
        }
    }
}
