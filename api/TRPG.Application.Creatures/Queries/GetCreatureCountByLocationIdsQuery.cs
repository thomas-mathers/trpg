using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureCountByLocationIdsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetCreatureCountByLocationIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureCountByLocationIdsQuery, int>
{
    public async Task<int> Handle(
        GetCreatureCountByLocationIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context.Creatures.CountAsync(
            c =>
                c.WorldId == query.WorldId
                && query.LocationIds.AsEnumerable().Contains(c.LocationId),
            cancellationToken
        );
}
