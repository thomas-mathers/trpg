using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class PropAddedMovementSpaceEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<PropAddedEvent>
{
    public async Task Handle(
        PropAddedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await coordinator.PostIfLoaded(
            domainEvent.WorldId,
            new InvalidateMovementSpace(domainEvent.LocationId),
            cancellationToken
        );
    }
}
