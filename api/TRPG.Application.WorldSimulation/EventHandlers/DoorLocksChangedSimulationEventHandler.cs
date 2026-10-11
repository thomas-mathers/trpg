using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class DoorLocksChangedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<DoorLocksChangedEvent>
{
    public async Task Handle(
        DoorLocksChangedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var locationId in domainEvent.LocationIds)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new RefreshLocationScenes(locationId),
                cancellationToken
            );
        }
    }
}
