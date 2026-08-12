using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts;
using TRPG.Data;

namespace TRPG.Application.Scenes.Queries;

internal enum LoreAnchorType
{
    Creature,
    Building,
    District,
    World,
    Country,
    State,
    City,
}

internal record LoreAnchorSummary(
    Guid Id,
    string Name,
    LoreAnchorType Type,
    string? Subtype,
    string Description
);

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

internal class GetLoreAnchorsByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetLoreAnchorsByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public static string CacheKey(Guid worldId) => $"namedEntities:{worldId}";

    public async Task<IReadOnlyCollection<LoreAnchorSummary>> Handle(
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

    private async Task<LoreAnchorSummary[]> BuildEntities(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var creatures = await context
            .Creatures.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new LoreAnchorSummary(
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
            .Select(b => new LoreAnchorSummary(
                b.Id,
                b.Name,
                LoreAnchorType.Building,
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
            .Select(d => new LoreAnchorSummary(
                d.Id,
                d.Name,
                LoreAnchorType.District,
                d.DistrictType.ToContract().ToDisplayName(),
                d.Description
            ))
            .ToArray();

        var world = await context
            .Worlds.AsNoTracking()
            .Where(w => w.Id == worldId)
            .Select(w => new LoreAnchorSummary(
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
            .Select(c => new LoreAnchorSummary(
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
            .Select(s => new LoreAnchorSummary(
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
            .Select(c => new LoreAnchorSummary(
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

internal class GetNamedEntitiesByWorldQueryHandler
{
    private readonly GetLoreAnchorsByWorldQueryHandler _getLoreAnchors;

    public GetNamedEntitiesByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
        : this(new GetLoreAnchorsByWorldQueryHandler(context, cache)) { }

    public GetNamedEntitiesByWorldQueryHandler(GetLoreAnchorsByWorldQueryHandler getLoreAnchors)
    {
        _getLoreAnchors = getLoreAnchors;
    }

    public static string CacheKey(Guid worldId) =>
        GetLoreAnchorsByWorldQueryHandler.CacheKey(worldId);

    public async Task<IReadOnlyCollection<NamedEntitySummary>> Handle(
        GetNamedEntitiesByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var anchors = await _getLoreAnchors.Handle(
            new GetLoreAnchorsByWorldQuery { WorldId = query.WorldId },
            cancellationToken
        );
        return anchors
            .Select(anchor => new NamedEntitySummary(
                anchor.Id,
                anchor.Name,
                (NamedEntityType)anchor.Type,
                anchor.Subtype,
                anchor.Description
            ))
            .ToArray();
    }
}

internal class GetNamedEntitiesByWorldQuery
{
    public required Guid WorldId { get; init; }
}
