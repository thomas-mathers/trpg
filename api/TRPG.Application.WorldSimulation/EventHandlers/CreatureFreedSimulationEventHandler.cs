using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreatureFreedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreatureFreedEvent>
{
    public Task Handle(
        CreatureFreedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        coordinator.Post(domainEvent.WorldId, new TrackCreature(domainEvent.CreatureId));

        return Task.CompletedTask;
    }
}
