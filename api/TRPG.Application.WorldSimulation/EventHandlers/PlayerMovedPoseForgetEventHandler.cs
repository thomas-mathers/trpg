using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class PlayerMovedPoseForgetEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(PlayerMovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        coordinator.Post(domainEvent.WorldId, new ForgetPlayerPose(domainEvent.PlayerId));

        return Task.CompletedTask;
    }
}
