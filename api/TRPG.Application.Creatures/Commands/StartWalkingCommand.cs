using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class StartWalkingCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class StartWalkingCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesStartedWalkingEvent> domainEvents
) : ICommandHandler<StartWalkingCommand>
{
    public async Task Handle(
        StartWalkingCommand command,
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
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(creature => creature.Activity, (CreatureActivity?)null)
                        .SetProperty(creature => creature.Posture, CreaturePosture.Standing)
                        .SetProperty(creature => creature.Movement, CreatureMovement.Walking),
                cancellationToken
            );
        await domainEvents.Publish(new CreaturesStartedWalkingEvent(livingIds), cancellationToken);
    }
}
