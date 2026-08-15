using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class AddCreatureJobCommand
{
    public required CreatureJob CreatureJob { get; init; }
}

public class AddCreatureJobCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddCreatureJobCommand>
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
