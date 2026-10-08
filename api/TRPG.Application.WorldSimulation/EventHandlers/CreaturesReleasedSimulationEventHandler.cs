using TRPG.Application.Common.Events;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreaturesReleasedSimulationEventHandler(
    WorldSimulationCoordinator coordinator
) : IDomainEventConsumer<CreaturesReleasedEvent>
{
    public Task Handle(
        CreaturesReleasedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var creatureId in domainEvent.CreatureIds)
        {
            coordinator.Post(domainEvent.WorldId, new ReleaseCreature(creatureId));
        }

        return Task.CompletedTask;
    }
}
