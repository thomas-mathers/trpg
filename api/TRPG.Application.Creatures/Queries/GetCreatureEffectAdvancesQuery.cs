using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureEffectAdvancesQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
    public required GameInstant GameTime { get; init; }
    public IReadOnlyCollection<Guid> ExcludedCreatureIds { get; init; } = [];
}

internal class GetCreatureEffectAdvancesQueryHandler(
    ICreaturesDbContext context,
    IQueryHandler<
        GetInventoryItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getInventoryByOwners,
    CreatureEffectAdvancer advancer
) : IQueryHandler<GetCreatureEffectAdvancesQuery, IReadOnlyCollection<CreatureEffectAdvance>>
{
    public async Task<IReadOnlyCollection<CreatureEffectAdvance>> Handle(
        GetCreatureEffectAdvancesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var loaded = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && query.LocationIds.AsEnumerable().Contains(creature.LocationId)
                && creature.Condition != CreatureCondition.Dead
                && !query.ExcludedCreatureIds.AsEnumerable().Contains(creature.Id)
            )
            .ToArrayAsync(cancellationToken);
        var creatures = loaded.Where(creature => creature.HasActiveEffects).ToArray();
        if (creatures.Length == 0)
        {
            return [];
        }

        var items = await getInventoryByOwners.Handle(
            new GetInventoryItemsByOwnersQuery
            {
                CreatureIds = creatures.Select(creature => creature.Id).ToArray(),
            },
            cancellationToken
        );

        return
        [
            .. creatures
                .Select(creature => Advance(creature, items, query.GameTime))
                .OfType<CreatureEffectAdvance>(),
        ];
    }

    private CreatureEffectAdvance? Advance(
        Creature creature,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>> items,
        GameInstant now
    )
    {
        var equipped = items
            .GetValueOrDefault(creature.Id, [])
            .Where(item => item.Ownership.EquippedSlot != null)
            .ToArray();
        var advanced = advancer.Advance(creature, equipped, now);

        return advanced is null
            ? null
            : new CreatureEffectAdvance(creature.LocationId, advanced.Update, advanced.Vitals);
    }
}
