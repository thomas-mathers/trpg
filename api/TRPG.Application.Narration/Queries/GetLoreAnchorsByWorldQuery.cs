using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Handling;
using TRPG.Application.Narration.Mappers;
using TRPG.Data;

namespace TRPG.Application.Narration.Queries;

public enum LoreAnchorType
{
    Creature,
    Building,
    District,
    World,
    Country,
    State,
    City,
}

public record LoreAnchorResult(
    Guid Id,
    string Name,
    LoreAnchorType Type,
    string? Subtype,
    string Description
);

public class GetLoreAnchorsByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetLoreAnchorsByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetLoreAnchorsByWorldQuery, IReadOnlyCollection<LoreAnchorResult>>
{
    public static string CacheKey(Guid worldId) => $"namedEntities:{worldId}";

    public async Task<IReadOnlyCollection<LoreAnchorResult>> Handle(
        GetLoreAnchorsByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await cache.GetOrCreateAsync(
            CacheKey(query.WorldId),
            async _ => await BuildEntities(query.WorldId, cancellationToken)
        );
        return entities ?? [];
    }

    private async Task<LoreAnchorResult[]> BuildEntities(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var creatures = await context
            .Creatures.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.Creature,
                c.CreatureType.ToString(),
                c.Biography
            ))
            .ToArrayAsync(cancellationToken);

        var buildingRows = await context
            .Buildings.AsNoTracking()
            .Where(b => b.WorldId == worldId)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.BuildingType,
                b.Description,
            })
            .ToArrayAsync(cancellationToken);
        var buildings = buildingRows
            .Select(b => new LoreAnchorResult(
                b.Id,
                b.Name,
                LoreAnchorType.Building,
                b.BuildingType.ToDisplayName(),
                b.Description
            ))
            .ToArray();

        var districtRows = await context
            .Districts.AsNoTracking()
            .Where(d => d.WorldId == worldId)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.DistrictType,
                d.Description,
            })
            .ToArrayAsync(cancellationToken);
        var districts = districtRows
            .Select(d => new LoreAnchorResult(
                d.Id,
                d.Name,
                LoreAnchorType.District,
                d.DistrictType.ToDisplayName(),
                d.Description
            ))
            .ToArray();

        var world = await context
            .Worlds.AsNoTracking()
            .Where(w => w.Id == worldId)
            .Select(w => new LoreAnchorResult(
                w.Id,
                w.Name,
                LoreAnchorType.World,
                null,
                w.Description
            ))
            .ToArrayAsync(cancellationToken);

        var countries = await context
            .Countries.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.Country,
                c.Focus.ToString(),
                c.Description
            ))
            .ToArrayAsync(cancellationToken);

        var states = await context
            .States.AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .Select(s => new LoreAnchorResult(
                s.Id,
                s.Name,
                LoreAnchorType.State,
                null,
                s.Description
            ))
            .ToArrayAsync(cancellationToken);

        var cities = await context
            .Cities.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.City,
                null,
                c.Description
            ))
            .ToArrayAsync(cancellationToken);

        return creatures
            .Concat(buildings)
            .Concat(districts)
            .Concat(world)
            .Concat(countries)
            .Concat(states)
            .Concat(cities)
            .ToArray();
    }
}
