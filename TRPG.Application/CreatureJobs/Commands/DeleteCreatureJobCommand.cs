using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.CreatureJobs.Commands;

internal class DeleteCreatureJobCommand
{
    public required Guid Id { get; init; }
}

internal class DeleteCreatureJobCommandHandler(TrpgDbContext context)
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
