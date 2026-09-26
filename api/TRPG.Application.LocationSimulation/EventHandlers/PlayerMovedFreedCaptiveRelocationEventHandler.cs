using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class PlayerMovedFreedCaptiveRelocationEventHandler(
    ICommandHandler<RelocateFreedCaptivesCommand> relocateFreedCaptives
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        relocateFreedCaptives.Handle(
            new RelocateFreedCaptivesCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
                LocationId = domainEvent.FromLocationId,
                GameTime = domainEvent.GameTime,
            },
            cancellationToken
        );
}
