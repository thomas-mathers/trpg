using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Buildings.Commands;

public class SetDoorTimedLockCommand
{
    public required Guid DoorConnectorId { get; init; }
    public required TimeSpan UnlocksAtPlaytime { get; init; }
}

internal class SetDoorTimedLockCommandHandler(TrpgDbContext context)
    : ICommandHandler<SetDoorTimedLockCommand>
{
    public async Task Handle(
        SetDoorTimedLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .DoorConnectors.Where(d => d.Id == command.DoorConnectorId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(d => d.IsLocked, true)
                        .SetProperty(d => d.UnlocksAtPlaytime, command.UnlocksAtPlaytime),
                cancellationToken
            );
    }
}
