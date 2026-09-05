using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class SetBedAssignmentCommand
{
    public required IReadOnlyCollection<Guid> BedIds { get; init; }
    public required Guid? AssignedCreatureId { get; init; }
}

internal class SetBedAssignmentCommandHandler(IPropsDbContext context)
    : ICommandHandler<SetBedAssignmentCommand>
{
    public async Task Handle(
        SetBedAssignmentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.BedIds.Count == 0)
        {
            return;
        }

        await context
            .Props.OfType<Bed>()
            .Where(bed => command.BedIds.AsEnumerable().Contains(bed.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(bed => bed.AssignedCreatureId, command.AssignedCreatureId),
                cancellationToken
            );
    }
}
