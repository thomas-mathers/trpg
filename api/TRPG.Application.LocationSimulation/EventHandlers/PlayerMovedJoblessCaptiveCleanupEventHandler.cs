using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class PlayerMovedJoblessCaptiveCleanupEventHandler(
    ICommandHandler<RemoveJoblessFreedCaptivesCommand> removeJoblessFreedCaptives
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        removeJoblessFreedCaptives.Handle(
            new RemoveJoblessFreedCaptivesCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
                LocationId = domainEvent.FromLocationId,
            },
            cancellationToken
        );
}
