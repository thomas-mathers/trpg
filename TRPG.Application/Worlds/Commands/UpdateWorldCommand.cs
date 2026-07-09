using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Commands;

public class UpdateWorldCommand
{
    public required World World { get; init; }
}

public class UpdateWorldCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UpdateWorldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.Worlds.Update(command.World);
        await context.SaveChangesAsync(cancellationToken);
    }
}
