using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreaturesByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCreaturesByIdsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Creature>> Handle(
        GetCreaturesByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(c => query.Ids.Contains(c.Id))
            .ToArrayAsync(cancellationToken);
    }
}
