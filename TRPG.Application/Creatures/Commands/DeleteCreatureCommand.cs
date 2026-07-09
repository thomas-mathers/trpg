using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Commands;

internal class DeleteCreatureCommand
{
    public required Guid Id { get; init; }
}

internal class DeleteCreatureCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        DeleteCreatureCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(p => p.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
