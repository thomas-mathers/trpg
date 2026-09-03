using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class SetBedOccupantCommand
{
    public required Guid BedId { get; init; }
    public required Guid? OccupantId { get; init; }
}

internal class SetBedOccupantCommandHandler(IPropsDbContext context)
    : ICommandHandler<SetBedOccupantCommand>
{
    public async Task Handle(
        SetBedOccupantCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Props.OfType<Bed>()
            .Where(b => b.Id == command.BedId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.OccupantId, command.OccupantId),
                cancellationToken
            );
    }
}
