using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyWorkstationCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal sealed class TryOccupyWorkstationCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyWorkstationCommand, bool>
{
    public async Task<bool> Handle(
        TryOccupyWorkstationCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Workstation>()
            .Where(workstation =>
                workstation.Id == command.WorkstationId && workstation.OccupantId == null
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(workstation => workstation.OccupantId, command.CreatureId),
                cancellationToken
            ) == 1;
}
