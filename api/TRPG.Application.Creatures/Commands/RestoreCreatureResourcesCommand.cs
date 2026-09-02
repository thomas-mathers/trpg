using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Commands;

public class RestoreCreatureResourcesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class RestoreCreatureResourcesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<RestoreCreatureResourcesCommand>
{
    public async Task Handle(
        RestoreCreatureResourcesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(c => c.CurrentHp, c => c.MaximumHp)
                        .SetProperty(c => c.CurrentAp, c => c.MaximumAp)
                        .SetProperty(c => c.CurrentMp, c => c.MaximumMp),
                cancellationToken
            );
    }
}
