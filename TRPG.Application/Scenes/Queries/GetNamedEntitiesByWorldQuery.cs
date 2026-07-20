using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal enum NamedEntityType
{
    Creature,
    Building,
    District,
    Item,
    World,
    Country,
    State,
    City,
}

internal record NamedEntitySummary(Guid Id, string Name, NamedEntityType Type);

internal class GetNamedEntitiesByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetNamedEntitiesByWorldQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<NamedEntitySummary>> Handle(
        GetNamedEntitiesByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await cache.GetOrCreateAsync(
            $"namedEntities:{query.WorldId}",
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
            .Select(c => new NamedEntitySummary(c.Id, c.Name, NamedEntityType.Creature))
            .ToArrayAsync(cancellationToken);

        var buildings = await context
            .Buildings.AsNoTracking()
            .Where(b => b.WorldId == worldId)
            .Select(b => new NamedEntitySummary(b.Id, b.Name, NamedEntityType.Building))
            .ToArrayAsync(cancellationToken);

        var districts = await context
            .Districts.AsNoTracking()
            .Where(d => d.WorldId == worldId)
            .Select(d => new NamedEntitySummary(d.Id, d.Name, NamedEntityType.District))
            .ToArrayAsync(cancellationToken);

        var uniqueItems = await context
            .Items.AsNoTracking()
            .Where(i => i.WorldId == worldId && i.Rarity == ItemRarity.Unique)
            .Select(i => new NamedEntitySummary(i.Id, i.Name, NamedEntityType.Item))
            .ToArrayAsync(cancellationToken);

        var world = await context
            .Worlds.AsNoTracking()
            .Where(w => w.Id == worldId)
            .Select(w => new NamedEntitySummary(w.Id, w.Name, NamedEntityType.World))
            .ToArrayAsync(cancellationToken);

        var countries = await context
            .Countries.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new NamedEntitySummary(c.Id, c.Name, NamedEntityType.Country))
            .ToArrayAsync(cancellationToken);

        var states = await context
            .States.AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .Select(s => new NamedEntitySummary(s.Id, s.Name, NamedEntityType.State))
            .ToArrayAsync(cancellationToken);

        var cities = await context
            .Cities.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .Select(c => new NamedEntitySummary(c.Id, c.Name, NamedEntityType.City))
            .ToArrayAsync(cancellationToken);

        return creatures
            .Concat(buildings)
            .Concat(districts)
            .Concat(uniqueItems)
            .Concat(world)
            .Concat(countries)
            .Concat(states)
            .Concat(cities)
            .ToArray();
    }
}
