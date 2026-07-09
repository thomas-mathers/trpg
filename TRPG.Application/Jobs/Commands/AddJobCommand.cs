using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Commands;

internal class AddJobCommand
{
    public required Job Job { get; init; }
}

internal class AddJobCommandHandler(TrpgDbContext context)
{
    public async Task Handle(AddJobCommand command, CancellationToken cancellationToken = default)
    {
        context.Jobs.Add(command.Job);
        await context.SaveChangesAsync(cancellationToken);
    }
}
