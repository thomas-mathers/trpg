using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreaturesByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCreaturesByIdsQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>>
{
    public async Task<IReadOnlyDictionary<Guid, Creature>> Handle(
        GetCreaturesByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(c => query.Ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
    }
}
