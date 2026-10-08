using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

public class GetSceneCreaturesQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetSceneCreaturesQueryHandler(
    IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>> getNearbyCreatures,
    IQueryHandler<
        GetEquippedItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getEquippedItemsByOwners,
    SceneCreatureInfoBuilder creatureInfoBuilder
) : IQueryHandler<GetSceneCreaturesQuery, IReadOnlyCollection<SceneCreatureInfo>>
{
    public async Task<IReadOnlyCollection<SceneCreatureInfo>> Handle(
        GetSceneCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creaturesHere = await getNearbyCreatures.Handle(
            new GetNearbyCreaturesQuery { PlayerId = query.PlayerId },
            cancellationToken
        );
        var player = creaturesHere.Single(creature => creature.Id == query.PlayerId);
        var requested = creaturesHere
            .Where(creature => creature.Id != query.PlayerId)
            .Where(creature => query.CreatureIds.Contains(creature.Id))
            .ToArray();
        var equippedItemsByCreature = await getEquippedItemsByOwners.Handle(
            new GetEquippedItemsByOwnersQuery
            {
                CreatureIds = requested.Select(creature => creature.Id).ToArray(),
            },
            cancellationToken
        );

        return await creatureInfoBuilder.BuildNearbyPeopleInfos(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = GameClock.GetCurrentInGameDate(query.GameTime),
                GameTime = query.GameTime,
            },
            player.LocationId,
            requested,
            equippedItemsByCreature,
            cancellationToken
        );
    }
}
