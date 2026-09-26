using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.Creatures.Commands;

public class SetCreatureRestedUntilCommand
{
    public required Guid CreatureId { get; init; }
    public required GameInstant RestedUntilGameTime { get; init; }
}

internal class SetCreatureRestedUntilCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<SetCreatureRestedUntilCommand>
{
    public async Task Handle(
        SetCreatureRestedUntilCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(c => c.Id == command.CreatureId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.RestedUntilGameTime, command.RestedUntilGameTime),
                cancellationToken
            );
    }
}
