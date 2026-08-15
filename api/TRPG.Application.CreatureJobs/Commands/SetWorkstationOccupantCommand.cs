using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class SetWorkstationOccupantCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid? OccupantId { get; init; }
}

public class SetWorkstationOccupantCommandHandler(TrpgDbContext context)
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
