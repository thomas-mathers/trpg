using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class ClearWorkstationOccupantsCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ClearWorkstationOccupantsCommandHandler(IPropsDbContext context)
    : ICommandHandler<ClearWorkstationOccupantsCommand>
{
    public async Task Handle(
        ClearWorkstationOccupantsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        await context
            .Props.OfType<Workstation>()
            .Where(workstation =>
                workstation.OccupantId != null
                && command.CreatureIds.AsEnumerable().Contains(workstation.OccupantId.Value)
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(workstation => workstation.OccupantId, (Guid?)null),
                cancellationToken
            );
    }
}
