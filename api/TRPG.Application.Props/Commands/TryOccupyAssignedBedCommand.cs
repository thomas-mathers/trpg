using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class TryOccupyAssignedBedCommand
{
    public required Guid BedId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class TryOccupyAssignedBedCommandHandler(IPropsDbContext context)
    : ICommandHandler<TryOccupyAssignedBedCommand, bool>
{
    public async Task<bool> Handle(
        TryOccupyAssignedBedCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Bed>()
            .Where(bed =>
                bed.Id == command.BedId
                && bed.AssignedCreatureId == command.CreatureId
                && bed.OccupantId == null
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(bed => bed.OccupantId, command.CreatureId),
                cancellationToken
            ) == 1;
}
