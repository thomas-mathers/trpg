using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class SetWorkstationOccupantCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid? OccupantId { get; init; }
}

internal class SetWorkstationOccupantCommandHandler(IPropsDbContext context)
    : ICommandHandler<SetWorkstationOccupantCommand>
{
    public async Task Handle(
        SetWorkstationOccupantCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Props.OfType<Workstation>()
            .Where(w => w.Id == command.WorkstationId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(w => w.OccupantId, command.OccupantId),
                cancellationToken
            );
    }
}
