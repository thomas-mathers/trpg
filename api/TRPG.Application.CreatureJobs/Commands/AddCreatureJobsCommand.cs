using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class AddCreatureJobsCommand
{
    public required IReadOnlyCollection<CreatureJob> Jobs { get; init; }
}

internal class AddCreatureJobsCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddCreatureJobsCommand>
{
    public async Task Handle(
        AddCreatureJobsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.CreatureJobs.AddRange(command.Jobs);
        await context.SaveChangesAsync(cancellationToken);
    }
}
