using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class AddDoorConnectorKeyCommand
{
    public required DoorConnectorKey DoorConnectorKey { get; init; }
}

internal class AddDoorConnectorKeyCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddDoorConnectorKeyCommand>
{
    public async Task Handle(
        AddDoorConnectorKeyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        context.DoorConnectorKeys.Add(command.DoorConnectorKey);
        await context.SaveChangesAsync(cancellationToken);
    }
}
