using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class AlertCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class AlertCreaturesCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<AlertCreaturesCommand>
{
    public async Task Handle(
        AlertCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition == CreatureCondition.Awake
                && !creature.IsAlerted
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.IsAlerted, true),
                cancellationToken
            );
    }
}
