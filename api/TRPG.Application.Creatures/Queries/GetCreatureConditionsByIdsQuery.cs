using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureConditionsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCreatureConditionsByIdsQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureConditionsByIdsQuery, IReadOnlyDictionary<Guid, CreatureCondition>>
{
    public async Task<IReadOnlyDictionary<Guid, CreatureCondition>> Handle(
        GetCreatureConditionsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => query.Ids.AsEnumerable().Contains(creature.Id))
            .ToDictionaryAsync(
                creature => creature.Id,
                creature => creature.Condition,
                cancellationToken
            );
}
