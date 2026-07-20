using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetEntityNamesByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetEntityNamesByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<string>> Handle(
        GetEntityNamesByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var names = await cache.GetOrCreateAsync(
            $"entityNames:{query.WorldId}",
            async _ => await BuildNames(query.WorldId, cancellationToken)
        );
        return names ?? [];
    }

    private async Task<string[]> BuildNames(Guid worldId, CancellationToken cancellationToken)
    {
        var creatureNames = await context
            .Creatures.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => c.Name)
            .ToArrayAsync(cancellationToken);

        var buildingNames = await context
            .Buildings.AsNoTracking()
            .Where(b => b.WorldId == worldId)
            .Select(b => b.Name)
            .ToArrayAsync(cancellationToken);

        var districtNames = await context
            .Districts.AsNoTracking()
            .Where(d => d.WorldId == worldId)
            .Select(d => d.Name)
            .ToArrayAsync(cancellationToken);

        var uniqueItemNames = await context
            .Items.AsNoTracking()
            .Where(i => i.WorldId == worldId && i.Rarity == ItemRarity.Unique)
            .Select(i => i.Name)
            .ToArrayAsync(cancellationToken);

        return creatureNames
            .Concat(buildingNames)
            .Concat(districtNames)
            .Concat(uniqueItemNames)
            .ToArray();
    }
}
