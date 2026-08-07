using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetNearbyCorpsesQuery
{
    public required Guid PlayerId { get; init; }
}

internal record CorpseSummary(Guid Id, string Name, int ItemCount);

internal class GetNearbyCorpsesQueryHandler(
    TrpgDbContext context,
    GetNearbyCreaturesQueryHandler getNearbyCreatures
)
{
    public async Task<IReadOnlyList<CorpseSummary>> Handle(
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

        var itemCountsByOwner = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && corpseIds.Contains(i.Ownership.OwnerId)
                && i.Quantity > 0
            )
            .GroupBy(i => i.Ownership.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OwnerId, g => g.Count, cancellationToken);

        return corpses
            .Select(c => new CorpseSummary(
                c.Id,
                c.Name,
                itemCountsByOwner.GetValueOrDefault(c.Id, 0)
            ))
            .ToArray();
    }
}
