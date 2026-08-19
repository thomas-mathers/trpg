using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Buildings.Commands;

public class ClearDoorTimedLockCommand
{
    public required Guid DoorConnectorId { get; init; }
}

internal class ClearDoorTimedLockCommandHandler(TrpgDbContext context)
    : ICommandHandler<ClearDoorTimedLockCommand>
{
    public async Task Handle(
        ClearDoorTimedLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .DoorConnectors.Where(d => d.Id == command.DoorConnectorId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(d => d.IsLocked, false)
                        .SetProperty(d => d.UnlocksAtPlaytime, (TimeSpan?)null),
                cancellationToken
            );
    }
}
