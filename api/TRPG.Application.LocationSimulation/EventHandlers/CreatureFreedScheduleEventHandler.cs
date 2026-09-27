using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class CreatureFreedScheduleEventHandler(
    IQueryHandler<GetGameTimeByWorldIdQuery, GameInstant> getGameTimeByWorldId,
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
        var gameTime = await getGameTimeByWorldId.Handle(
            new GetGameTimeByWorldIdQuery { WorldId = domainEvent.WorldId },
            cancellationToken
        );
        await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = [domainEvent.CreatureId],
                GameTime = gameTime,
                BecameAvailableAtGameTimeByCreatureId = new Dictionary<Guid, GameInstant>
                {
                    [domainEvent.CreatureId] = gameTime,
                },
            },
            cancellationToken
        );
    }
}
