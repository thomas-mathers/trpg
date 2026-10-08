using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesSpawnedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesSpawnedEvent>
{
    public Task Handle(
        CreaturesSpawnedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            coordinator.Post(domainEvent.WorldId, new TrackCreature(creatureId));
        }

        return Task.CompletedTask;
    }
}
