using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Commands;

internal class UpdateJobCommand
{
    public required Job Job { get; init; }
}

internal class UpdateJobCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UpdateJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Jobs.Update(command.Job);
        await context.SaveChangesAsync(cancellationToken);
    }
}
