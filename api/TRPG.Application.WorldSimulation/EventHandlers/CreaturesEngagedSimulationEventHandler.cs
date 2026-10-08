using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesEngagedSimulationEventHandler(WorldSimulationCoordinator coordinator)
    : IDomainEventConsumer<CreaturesEngagedEvent>
{
    public Task Handle(
        CreaturesEngagedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            coordinator.Post(domainEvent.WorldId, new EngageCreature(creatureId));
        }

        return Task.CompletedTask;
    }
}
