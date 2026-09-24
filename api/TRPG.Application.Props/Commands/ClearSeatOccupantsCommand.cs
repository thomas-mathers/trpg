using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class ClearSeatOccupantsCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ClearSeatOccupantsCommandHandler(IPropsDbContext context)
    : ICommandHandler<ClearSeatOccupantsCommand>
{
    public async Task Handle(
        ClearSeatOccupantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        await context
            .Props.OfType<Seat>()
            .Where(seat =>
                seat.OccupantId != null
                && command.CreatureIds.AsEnumerable().Contains(seat.OccupantId.Value)
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(seat => seat.OccupantId, (Guid?)null),
                cancellationToken
            );
    }
}
