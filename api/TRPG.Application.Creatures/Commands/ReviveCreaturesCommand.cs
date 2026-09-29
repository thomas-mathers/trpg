using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ReviveCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ReviveCreaturesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<ReviveCreaturesCommand>
{
    public async Task Handle(
        ReviveCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition == CreatureCondition.Dead
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(creature => creature.Posture, CreaturePosture.Standing),
                cancellationToken
            );
    }
}
