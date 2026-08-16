using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Commands;

public class AddCreatureJobCommand
{
    public required CreatureJob CreatureJob { get; init; }
}

internal class AddCreatureJobCommandHandler(TrpgDbContext context)
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
