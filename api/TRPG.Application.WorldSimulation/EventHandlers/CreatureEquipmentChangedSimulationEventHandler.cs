using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class CreatureEquipmentChangedSimulationEventHandler(
    WorldSimulationCoordinator coordinator,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById
) : IDomainEventConsumer<CreatureEquipmentChangedEvent>
{
    public async Task Handle(
        CreatureEquipmentChangedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = domainEvent.CreatureId },
            cancellationToken
        );
        if (creature is null)
        {
            return;
        }

        await coordinator.PostIfLoaded(
            creature.WorldId,
            new RefreshCreatureStatus(creature.LocationId, creature.Id),
            cancellationToken
        );
    }
}
