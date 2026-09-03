using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Commands;

public class SetBedAssignmentCommand
{
    public required Guid BedId { get; init; }
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
        await context
            .Props.OfType<Bed>()
            .Where(b => b.Id == command.BedId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.AssignedCreatureId, command.AssignedCreatureId),
                cancellationToken
            );
    }
}
