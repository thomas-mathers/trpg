using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.CreatureJobs.Commands;

namespace TRPG.Application.CreatureJobs.EventHandlers;

internal sealed class CreatureFreedJobEventHandler(
    ICommandHandler<SyncCreatureToCurrentJobCommand> syncCreatureToCurrentJob
) : IDomainEventConsumer<CreatureFreedEvent>
{
    public Task Handle(
        CreatureFreedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        syncCreatureToCurrentJob.Handle(
            new SyncCreatureToCurrentJobCommand
            {
                WorldId = domainEvent.WorldId,
                CreatureId = domainEvent.CreatureId,
            },
            cancellationToken
        );
}
