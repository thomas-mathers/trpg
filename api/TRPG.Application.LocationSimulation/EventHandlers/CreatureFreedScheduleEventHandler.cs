using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class CreatureFreedScheduleEventHandler(
    ICommandHandler<
        SyncCreatureJobSchedulesCommand,
        SyncCreatureJobSchedulesResult
    > syncCreatureJobSchedules
) : IDomainEventConsumer<CreatureFreedEvent>
{
    public async Task Handle(
        CreatureFreedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [domainEvent.CreatureId],
                GameTime = domainEvent.GameTime,
                BecameAvailableAtGameTimeByCreatureId = new Dictionary<Guid, GameInstant>
                {
                    [domainEvent.CreatureId] = domainEvent.GameTime,
                },
            },
            cancellationToken
        );
    }
}
