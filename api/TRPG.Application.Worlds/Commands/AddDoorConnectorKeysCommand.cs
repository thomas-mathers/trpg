using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class AddDoorConnectorKeysCommand
{
    public required IReadOnlyCollection<DoorConnectorKey> DoorConnectorKeys { get; init; }
}

internal class AddDoorConnectorKeysCommandHandler(IWorldsDbContext context)
    : ICommandHandler<AddDoorConnectorKeysCommand>
{
    public async Task Handle(
        AddDoorConnectorKeysCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.DoorConnectorKeys.AddRange(command.DoorConnectorKeys);
        await context.SaveChangesAsync(cancellationToken);
    }
}
