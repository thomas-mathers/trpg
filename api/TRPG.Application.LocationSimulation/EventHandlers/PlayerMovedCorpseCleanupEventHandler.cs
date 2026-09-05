using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class PlayerMovedCorpseCleanupEventHandler(
    ICommandHandler<CleanUpAbandonedCorpsesCommand> cleanUpAbandonedCorpses
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        cleanUpAbandonedCorpses.Handle(
            new CleanUpAbandonedCorpsesCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
                LocationId = domainEvent.FromLocationId,
            },
            cancellationToken
        );
}
