using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesDiedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesDiedEvent>
{
    public Task Handle(
        CreaturesDiedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            coordinator.Post(domainEvent.WorldId, new RemoveCreature(creatureId));
        }

        return Task.CompletedTask;
    }
}
