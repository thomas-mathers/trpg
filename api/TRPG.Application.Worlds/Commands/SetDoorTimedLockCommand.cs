using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Worlds.Commands;

public class SetDoorTimedLockCommand
{
    public required IReadOnlyCollection<Guid> DoorConnectorIds { get; init; }
    public required GameInstant? UnlocksAtGameTime { get; init; }
}

internal class SetDoorTimedLockCommandHandler(IWorldsDbContext context)
    : ICommandHandler<SetDoorTimedLockCommand>
{
    public async Task Handle(
        SetDoorTimedLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .DoorConnectors.Where(d => command.DoorConnectorIds.Contains(d.Id))
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(d => d.IsLocked, command.UnlocksAtGameTime != null)
                        .SetProperty(d => d.UnlocksAtGameTime, command.UnlocksAtGameTime),
                cancellationToken
            );
    }
}
