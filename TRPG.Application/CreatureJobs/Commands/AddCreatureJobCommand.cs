using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Commands;

internal class AddCreatureJobCommand
{
    public required CreatureJob CreatureJob { get; init; }
}

internal class AddCreatureJobCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        AddCreatureJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.CreatureJobs.Add(command.CreatureJob);
        await context.SaveChangesAsync(cancellationToken);
    }
}
