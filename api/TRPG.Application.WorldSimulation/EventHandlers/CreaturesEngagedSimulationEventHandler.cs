using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesEngagedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesEngagedEvent>
{
    public async Task Handle(
        CreaturesEngagedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new EngageCreature(creatureId),
                cancellationToken
            );
        }
    }
}
