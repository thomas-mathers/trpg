using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Worlds.Commands;

public class SetWorldGameTimeCommand
{
    public required Guid WorldId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SetWorldGameTimeCommandHandler(IWorldsDbContext context)
    : ICommandHandler<SetWorldGameTimeCommand>
{
    public async Task Handle(
        SetWorldGameTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Worlds.Where(w => w.Id == command.WorldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(w => w.GameTime, command.GameTime),
                cancellationToken
            );
    }
}
