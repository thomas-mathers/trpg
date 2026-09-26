using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Routing.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class CreaturesReleasedEventHandler(
    ICommandHandler<
        ResumeReleasedRouteTravelersCommand,
        IReadOnlyCollection<Guid>
    > resumeReleasedRouteTravelers,
    ICommandHandler<
        SyncCreatureJobSchedulesCommand,
        SyncCreatureJobSchedulesResult
    > syncCreatureJobSchedules
) : IDomainEventConsumer<CreaturesReleasedEvent>
{
    public async Task Handle(
        CreaturesReleasedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var readyCreatureIds = await resumeReleasedRouteTravelers.Handle(
            new ResumeReleasedRouteTravelersCommand
            {
                ReleasedCreatureIds = domainEvent.CreatureIds,
                GameTime = domainEvent.GameTime,
            },
            cancellationToken
        );
        await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = readyCreatureIds,
                GameTime = domainEvent.GameTime,
                BecameAvailableAtGameTimeByCreatureId = readyCreatureIds.ToDictionary(
                    id => id,
                    _ => domainEvent.GameTime
                ),
            },
            cancellationToken
        );
    }
}
