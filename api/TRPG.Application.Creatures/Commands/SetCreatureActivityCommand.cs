using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class SetCreatureActivityCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required CreatureActivity? Activity { get; init; }
}

internal class SetCreatureActivityCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesWokeEvent> domainEvents
) : ICommandHandler<SetCreatureActivityCommand>
{
    public async Task Handle(
        SetCreatureActivityCommand command,
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

        await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition != CreatureCondition.Dead
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(creature => creature.Activity, command.Activity)
                        .SetProperty(
                            creature => creature.Posture,
                            creature =>
                                creature.Posture == CreaturePosture.Lying
                                    ? CreaturePosture.Standing
                                    : creature.Posture
                        )
                        .SetProperty(creature => creature.Movement, CreatureMovement.Stationary),
                cancellationToken
            );

        if (sleepingIds.Count > 0)
        {
            await domainEvents.Publish(new CreaturesWokeEvent(sleepingIds), cancellationToken);
        }
    }
}
