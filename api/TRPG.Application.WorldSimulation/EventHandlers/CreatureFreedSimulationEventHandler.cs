using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreatureFreedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreatureFreedEvent>
{
    public async Task Handle(
        CreatureFreedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await coordinator.PostIfLoaded(
            domainEvent.WorldId,
            new TrackCreature(domainEvent.CreatureId),
            cancellationToken
        );
    }
}
