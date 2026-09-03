using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Commands;

public class SetDoorConnectorLockedCommand
{
    public required Guid ConnectorId { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetDoorConnectorLockedCommandHandler(IWorldsDbContext context)
    : ICommandHandler<SetDoorConnectorLockedCommand>
{
    public async Task Handle(
        SetDoorConnectorLockedCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .DoorConnectors.Where(door => door.ConnectorId == command.ConnectorId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(door => door.IsLocked, command.IsLocked),
                cancellationToken
            );
}
