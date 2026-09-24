using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class StopSittingCommand
{
    public required Guid CreatureId { get; init; }
}

internal class StopSittingCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<StopSittingCommand>
{
    public async Task Handle(
        StopSittingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                creature.Id == command.CreatureId && creature.State == CreatureState.Sitting
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.State, CreatureState.Idle),
                cancellationToken
            );
    }
}
