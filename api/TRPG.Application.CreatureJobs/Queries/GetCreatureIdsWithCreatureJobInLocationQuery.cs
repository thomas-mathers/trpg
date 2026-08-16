using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureIdsWithCreatureJobInLocationQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetCreatureIdsWithCreatureJobInLocationQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureIdsWithCreatureJobInLocationQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsWithCreatureJobInLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .CreatureJobs.Where(j => j.LocationId == query.LocationId)
            .Select(j => j.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
