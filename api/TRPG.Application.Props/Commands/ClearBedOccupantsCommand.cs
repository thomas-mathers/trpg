using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class ClearBedOccupantsCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ClearBedOccupantsCommandHandler(IPropsDbContext context)
    : ICommandHandler<ClearBedOccupantsCommand>
{
    public async Task Handle(
        ClearBedOccupantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        await context
            .Props.OfType<Bed>()
            .Where(bed =>
                bed.OccupantId != null
                && command.CreatureIds.AsEnumerable().Contains(bed.OccupantId.Value)
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(bed => bed.OccupantId, (Guid?)null),
                cancellationToken
            );
    }
}
