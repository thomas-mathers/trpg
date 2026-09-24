using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class CreatureFreedScheduleEventHandler(
    IQueryHandler<GetPlaytimeByWorldIdQuery, TimeSpan> getPlaytimeByWorldId,
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
        var playtime = await getPlaytimeByWorldId.Handle(
            new GetPlaytimeByWorldIdQuery { WorldId = domainEvent.WorldId },
            cancellationToken
        );
        await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [domainEvent.CreatureId],
                Playtime = playtime,
                BecameAvailableAtPlaytimeByCreatureId = new Dictionary<Guid, TimeSpan>
                {
                    [domainEvent.CreatureId] = playtime,
                },
            },
            cancellationToken
        );
    }
}
