using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public sealed record CreaturePoseUpdate(
    Guid CreatureId,
    Guid LocationId,
    Guid? PreviousLocationId,
    CreatureMovement Movement,
    CreatureActivity? Activity,
    Point? StandAt = null,
    Guid? CurrentTravelNodeId = null
)
{
    public CreaturePoseUpdate Then(CreaturePoseUpdate next) =>
        next with
        {
            PreviousLocationId = next.PreviousLocationId ?? PreviousLocationId,
            StandAt = next.StandAt ?? StandAt,
            CurrentTravelNodeId = next.CurrentTravelNodeId ?? CurrentTravelNodeId,
        };
}

public class ApplyCreaturePoseUpdatesCommand
{
    public required IReadOnlyCollection<CreaturePoseUpdate> Updates { get; init; }
}

internal class ApplyCreaturePoseUpdatesCommandHandler(
    ICreaturesDbContext context,
    ICommandHandler<PlaceCreaturesAtLocationCommand> placeCreaturesAtLocation,
    IDomainEventPublisher<CreaturesStartedWalkingEvent> startedWalkingEvents,
    IDomainEventPublisher<CreaturesWokeEvent> wokeEvents
) : ICommandHandler<ApplyCreaturePoseUpdatesCommand>
{
    public async Task Handle(
        ApplyCreaturePoseUpdatesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Updates.Count == 0)
        {
            return;
        }

        var sleepingIds = await FindSleeping(command.Updates, cancellationToken);
        foreach (var update in command.Updates)
        {
            await Write(update, cancellationToken);
        }

        await PlaceRelocated(command.Updates, cancellationToken);

        await PublishEvents(command.Updates, sleepingIds, cancellationToken);
    }

    private async Task<List<Guid>> FindSleeping(
        IReadOnlyCollection<CreaturePoseUpdate> updates,
        CancellationToken cancellationToken
    )
    {
        var ids = updates.Select(update => update.CreatureId).ToList();

        return await context
            .Creatures.Where(creature =>
                ids.Contains(creature.Id) && creature.Condition == CreatureCondition.Sleeping
            )
            .Select(creature => creature.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task PublishEvents(
        IReadOnlyCollection<CreaturePoseUpdate> updates,
        IReadOnlyCollection<Guid> sleepingIds,
        CancellationToken cancellationToken
    )
    {
        if (sleepingIds.Count > 0)
        {
            await wokeEvents.Publish(new CreaturesWokeEvent(sleepingIds), cancellationToken);
        }

        var walkingIds = updates
            .Where(update => update.Movement == CreatureMovement.Walking)
            .Select(update => update.CreatureId)
            .ToList();
        if (walkingIds.Count > 0)
        {
            await startedWalkingEvents.Publish(
                new CreaturesStartedWalkingEvent(walkingIds),
                cancellationToken
            );
        }
    }

    private async Task Write(CreaturePoseUpdate update, CancellationToken cancellationToken)
    {
        var walking = update.Movement == CreatureMovement.Walking;
        var previousLocationId = update.PreviousLocationId;
        var standAt = update.StandAt;
        var currentTravelNodeId = update.CurrentTravelNodeId;

        await context
            .Creatures.Where(creature =>
                creature.Id == update.CreatureId && creature.Condition != CreatureCondition.Dead
            )
            .ExecuteUpdateAsync(
                setters =>
                {
                    setters
                        .SetProperty(creature => creature.LocationId, update.LocationId)
                        .SetProperty(creature => creature.Movement, update.Movement)
                        .SetProperty(creature => creature.Activity, update.Activity)
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(
                            creature => creature.Posture,
                            creature =>
                                walking || creature.Posture == CreaturePosture.Lying
                                    ? CreaturePosture.Standing
                                    : creature.Posture
                        );
                    if (standAt != null)
                    {
                        setters
                            .SetProperty(creature => creature.X, standAt.X)
                            .SetProperty(creature => creature.Y, standAt.Y);
                    }
                    if (previousLocationId != null)
                    {
                        setters.SetProperty(
                            creature => creature.PreviousLocationId,
                            previousLocationId
                        );
                    }
                    if (currentTravelNodeId != null)
                    {
                        setters.SetProperty(
                            creature => creature.CurrentTravelNodeId,
                            currentTravelNodeId
                        );
                    }
                },
                cancellationToken
            );
    }

    private async Task PlaceRelocated(
        IReadOnlyCollection<CreaturePoseUpdate> updates,
        CancellationToken cancellationToken
    )
    {
        var relocatedByLocation = updates
            .Where(update => update.PreviousLocationId != null && update.StandAt == null)
            .GroupBy(update => update.LocationId);

        foreach (var group in relocatedByLocation)
        {
            await placeCreaturesAtLocation.Handle(
                new PlaceCreaturesAtLocationCommand
                {
                    CreatureIds = [.. group.Select(update => update.CreatureId)],
                    LocationId = group.Key,
                },
                cancellationToken
            );
        }
    }
}
