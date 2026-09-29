using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class WakeCreaturesCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class WakeCreaturesCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesWokeEvent> domainEvents
) : ICommandHandler<WakeCreaturesCommand>
{
    public async Task Handle(
        WakeCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var sleepingIds = await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition == CreatureCondition.Sleeping
            )
            .Select(creature => creature.Id)
            .ToListAsync(cancellationToken);
        if (sleepingIds.Count == 0)
        {
            return;
        }

        await context
            .Creatures.Where(creature => sleepingIds.Contains(creature.Id))
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(creature => creature.Posture, CreaturePosture.Standing),
                cancellationToken
            );
        await domainEvents.Publish(new CreaturesWokeEvent(sleepingIds), cancellationToken);
    }
}
