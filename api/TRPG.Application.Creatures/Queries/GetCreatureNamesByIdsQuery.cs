using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureNamesByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCreatureNamesByIdsQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureNamesByIdsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> Handle(
        GetCreatureNamesByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => query.Ids.AsEnumerable().Contains(creature.Id))
            .ToDictionaryAsync(
                creature => creature.Id,
                creature => creature.Name,
                cancellationToken
            );
}
