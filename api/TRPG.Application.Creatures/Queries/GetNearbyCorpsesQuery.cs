using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetNearbyCorpsesQuery
{
    public required Guid PlayerId { get; init; }
}

public record CorpseResult(Guid Id, string Name, int ItemCount);

internal class GetNearbyCorpsesQueryHandler(
    IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>> getNearbyCreatures,
    IQueryHandler<GetItemCountsByOwnersQuery, IReadOnlyDictionary<Guid, int>> getItemCountsByOwners
) : IQueryHandler<GetNearbyCorpsesQuery, IReadOnlyList<CorpseResult>>
{
    public async Task<IReadOnlyList<CorpseResult>> Handle(
        GetNearbyCorpsesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var nearby = await getNearbyCreatures.Handle(
            new GetNearbyCreaturesQuery
            {
                PlayerId = query.PlayerId,
                ExcludingCreatureId = query.PlayerId,
            },
            cancellationToken
        );

        var corpses = nearby.Where(c => c.State == CreatureState.Dead).ToArray();
        var corpseIds = corpses.Select(c => c.Id).ToArray();

        var itemCountsByOwner = await getItemCountsByOwners.Handle(
            new GetItemCountsByOwnersQuery { OwnerIds = corpseIds, OwnerType = OwnerType.Creature },
            cancellationToken
        );

        return corpses
            .Select(c => new CorpseResult(
                c.Id,
                c.Name,
                itemCountsByOwner.GetValueOrDefault(c.Id, 0)
            ))
            .ToArray();
    }
}
