using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Commands;

public class CalmCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class CalmCreaturesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<CalmCreaturesCommand>
{
    public async Task Handle(
        CalmCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id) && creature.IsAlerted
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.IsAlerted, false),
                cancellationToken
            );
    }
}
