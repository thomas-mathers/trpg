using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesReleasedSimulationEventHandler(
    WorldSimulationCoordinator coordinator
) : IDomainEventConsumer<CreaturesReleasedEvent>
{
    public async Task Handle(
        CreaturesReleasedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new ReleaseCreature(creatureId),
                cancellationToken
            );
        }
    }
}
