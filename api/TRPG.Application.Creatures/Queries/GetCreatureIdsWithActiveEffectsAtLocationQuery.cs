using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureIdsWithActiveEffectsAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public IReadOnlyCollection<Guid> ExcludedCreatureIds { get; init; } = [];
}

internal class GetCreatureIdsWithActiveEffectsAtLocationQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureIdsWithActiveEffectsAtLocationQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetCreatureIdsWithActiveEffectsAtLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creatures = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && creature.LocationId == query.LocationId
                && creature.Condition != CreatureCondition.Dead
                && !query.ExcludedCreatureIds.AsEnumerable().Contains(creature.Id)
            )
            .ToArrayAsync(cancellationToken);

        return creatures
            .Where(creature => creature.HasActiveEffects)
            .Select(creature => creature.Id)
            .ToArray();
    }
}
