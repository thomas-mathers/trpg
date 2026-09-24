using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class TryStartSittingCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class TryStartSittingCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<TryStartSittingCommand, bool>
{
    public async Task<bool> Handle(
        TryStartSittingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updated = await context
            .Creatures.Where(creature =>
                creature.Id == command.CreatureId
                && creature.LocationId == command.LocationId
                && creature.State == CreatureState.Idle
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.State, CreatureState.Sitting),
                cancellationToken
            );

        return updated == 1;
    }
}
