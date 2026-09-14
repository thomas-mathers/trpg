using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.EventHandlers;

internal sealed class CreatureKilledQuestEventHandler(
    QuestObjectiveAdvancer questObjectiveAdvancer,
    IQueryHandler<GetBuildingIdByLocationIdQuery, Guid?> getBuildingIdByLocationId
) : IDomainEventConsumer<CreatureKilledEvent>
{
    public async Task Handle(
        CreatureKilledEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var buildingId = await getBuildingIdByLocationId.Handle(
            new GetBuildingIdByLocationIdQuery { LocationId = domainEvent.LocationId },
            cancellationToken
        );

        await questObjectiveAdvancer.Advance(
            domainEvent.PlayerId,
            domainEvent.WorldId,
            objective =>
                objective switch
                {
                    KillCreatureObjective kill => kill.CreatureId == domainEvent.CreatureId,
                    KillCreatureTypeObjective kill => kill.CreatureType == domainEvent.CreatureType,
                    ClearLocationObjective clear => buildingId != null
                        && clear.BuildingId == buildingId,
                    _ => false,
                },
            cancellationToken
        );
    }
}
