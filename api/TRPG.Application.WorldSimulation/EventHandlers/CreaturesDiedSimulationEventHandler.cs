using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesDiedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesDiedEvent>
{
    public async Task Handle(
        CreaturesDiedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new RemoveCreature(creatureId),
                cancellationToken
            );
        }
    }
}
