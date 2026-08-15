using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.CreatureJobs.Commands;

public class DeleteCreatureJobCommand
{
    public required Guid Id { get; init; }
}

internal class DeleteCreatureJobCommandHandler(TrpgDbContext context)
    : ICommandHandler<DeleteCreatureJobCommand>
{
    public async Task Handle(
        DeleteCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .CreatureJobs.Where(j => j.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
