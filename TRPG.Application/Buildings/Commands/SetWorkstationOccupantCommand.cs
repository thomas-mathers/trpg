using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Commands;

internal class SetWorkstationOccupantCommand
{
    public required Guid WorkstationId { get; init; }
    public required Guid? OccupantId { get; init; }
}

internal class SetWorkstationOccupantCommandHandler(TrpgDbContext context)
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
