using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Jobs.Commands;

internal class DeleteJobCommand
{
    public required Guid Id { get; init; }
}

internal class DeleteJobCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        DeleteJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context.Jobs.Where(j => j.Id == command.Id).ExecuteDeleteAsync(cancellationToken);
    }
}
