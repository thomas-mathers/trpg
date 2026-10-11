using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class QuestMarkersChangedSimulationEventHandler(
    WorldSimulationCoordinator coordinator,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>> getNearbyCreatures
) : IDomainEventConsumer<QuestMarkersChangedEvent>
{
    public async Task Handle(
        QuestMarkersChangedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await coordinator.PostIfLoaded(
            domainEvent.WorldId,
            new RefreshPlayerScene(domainEvent.PlayerId),
            cancellationToken
        );

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = domainEvent.PlayerId },
            cancellationToken
        );
        if (player is null)
        {
            return;
        }

        var nearby = await getNearbyCreatures.Handle(
            new GetNearbyCreaturesQuery
            {
                PlayerId = player.Id,
                ExcludingCreatureId = player.Id,
                IncludeDead = false,
            },
            cancellationToken
        );
        foreach (var creature in nearby)
        {
            await coordinator.PostIfLoaded(
                domainEvent.WorldId,
                new RefreshCreatureStatus(player.LocationId, creature.Id),
                cancellationToken
            );
        }
    }
}
