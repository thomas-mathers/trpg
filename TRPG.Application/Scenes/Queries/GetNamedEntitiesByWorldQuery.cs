using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

        var buildings = await context
            .Buildings.AsNoTracking()
            .Where(b => b.WorldId == worldId)
            .Select(b => new NamedEntitySummary(
                b.Id,
                b.Name,
                NamedEntityType.Building,
                b.BuildingType.ToString(),
                b.Description
            ))
            .ToArrayAsync(cancellationToken);

        var districts = await context
            .Districts.AsNoTracking()
            .Where(d => d.WorldId == worldId)
            .Select(d => new NamedEntitySummary(
                d.Id,
                d.Name,
                NamedEntityType.District,
                d.DistrictType.ToString(),
                d.Description
            ))
            .ToArrayAsync(cancellationToken);

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
