using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class PutCreaturesToSleepCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class PutCreaturesToSleepCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesFellAsleepEvent> domainEvents
) : ICommandHandler<PutCreaturesToSleepCommand>
{
    public async Task Handle(
        PutCreaturesToSleepCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var livingIds = await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition != CreatureCondition.Dead
            )
            .Select(creature => creature.Id)
            .ToListAsync(cancellationToken);
        if (livingIds.Count == 0)
        {
            return;
        }

        await context
            .Creatures.Where(creature => livingIds.Contains(creature.Id))
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Condition, CreatureCondition.Sleeping)
                        .SetProperty(creature => creature.Activity, (CreatureActivity?)null)
                        .SetProperty(creature => creature.Posture, CreaturePosture.Lying)
                        .SetProperty(creature => creature.Movement, CreatureMovement.Stationary)
                        .SetProperty(creature => creature.IsAlerted, false)
                        .SetProperty(creature => creature.IsSneaking, false),
                cancellationToken
            );
        await domainEvents.Publish(new CreaturesFellAsleepEvent(livingIds), cancellationToken);
    }
}
