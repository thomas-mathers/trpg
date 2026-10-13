using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class PlayerMovedPoseSeedEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<PlayerMovedEvent>
{
    public async Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await coordinator.PostIfLoaded(
            domainEvent.WorldId,
            new SeedPlayerPose(domainEvent.PlayerId),
            cancellationToken
        );
    }
}
