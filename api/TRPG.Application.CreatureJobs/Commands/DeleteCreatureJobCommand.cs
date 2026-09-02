using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.CreatureJobs.Commands;

public class DeleteCreatureJobCommand
{
    public required Guid Id { get; init; }
}

internal class DeleteCreatureJobCommandHandler(ICreatureJobsDbContext context)
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
