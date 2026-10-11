using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesSpawnedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesSpawnedEvent>
{
    public async Task Handle(
        CreaturesSpawnedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new TrackCreature(creatureId),
                cancellationToken
            );
        }
    }
}
