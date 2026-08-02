using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts;
using TRPG.Data;

namespace TRPG.Application.Scenes.Queries;

internal enum NamedEntityType
{
    Creature,
    Building,
    District,
    World,
    Country,
    State,
    City,
}

internal record NamedEntitySummary(
    Guid Id,
    string Name,
    NamedEntityType Type,
    string? Subtype,
    string Description
);

internal class GetNamedEntitiesByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetNamedEntitiesByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public static string CacheKey(Guid worldId) => $"namedEntities:{worldId}";

    public async Task<IReadOnlyCollection<NamedEntitySummary>> Handle(
        GetNamedEntitiesByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await cache.GetOrCreateAsync(
            CacheKey(query.WorldId),
            async _ => await BuildEntities(query.WorldId, cancellationToken)
        );
        return entities ?? [];
    }

    private async Task<NamedEntitySummary[]> BuildEntities(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var creatures = await context
            .Creatures.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new NamedEntitySummary(
                c.Id,
                c.Name,
                NamedEntityType.Creature,
                c.CreatureType.ToString(),
                c.Biography
            ))
            .ToArrayAsync(cancellationToken);

        // BuildingType/DistrictType display names come from [Description] attributes read via
        // reflection (ToDisplayName), which EF Core can't translate to SQL - fetch the raw rows
        // first, then humanize in memory.
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
            .Select(b => new NamedEntitySummary(
                b.Id,
                b.Name,
                NamedEntityType.Building,
                b.BuildingType.ToContract().ToDisplayName(),
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
            .Select(d => new NamedEntitySummary(
                d.Id,
                d.Name,
                NamedEntityType.District,
                d.DistrictType.ToContract().ToDisplayName(),
                d.Description
            ))
            .ToArray();

        var world = await context
            .Worlds.AsNoTracking()
            .Where(w => w.Id == worldId)
            .Select(w => new NamedEntitySummary(
                w.Id,
                w.Name,
                NamedEntityType.World,
                null,
                w.Description
            ))
            .ToArrayAsync(cancellationToken);

        var countries = await context
            .Countries.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new NamedEntitySummary(
                c.Id,
                c.Name,
                NamedEntityType.Country,
                c.Focus.ToString(),
                c.Description
            ))
            .ToArrayAsync(cancellationToken);

        var states = await context
            .States.AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .Select(s => new NamedEntitySummary(
                s.Id,
                s.Name,
                NamedEntityType.State,
                null,
                s.Description
            ))
            .ToArrayAsync(cancellationToken);

        var cities = await context
            .Cities.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new NamedEntitySummary(
                c.Id,
                c.Name,
                NamedEntityType.City,
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
