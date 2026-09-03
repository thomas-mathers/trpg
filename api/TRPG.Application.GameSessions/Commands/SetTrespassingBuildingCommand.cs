using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Commands;

public class SetTrespassingBuildingCommand
{
    public required Guid WorldId { get; init; }
    public required Guid? BuildingId { get; init; }
}

internal class SetTrespassingBuildingCommandHandler(IGameSessionsDbContext context)
    : ICommandHandler<SetTrespassingBuildingCommand>
{
    public async Task Handle(
        SetTrespassingBuildingCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .GameSessions.Where(s => s.WorldId == command.WorldId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(session => session.TrespassingBuildingId, command.BuildingId),
                cancellationToken
            );
}
