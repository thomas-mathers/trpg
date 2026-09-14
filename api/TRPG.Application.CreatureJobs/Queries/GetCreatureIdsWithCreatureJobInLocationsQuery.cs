using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureIdsWithCreatureJobInLocationsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetCreatureIdsWithCreatureJobInLocationsQueryHandler(ICreatureJobsDbContext context)
    : IQueryHandler<GetCreatureIdsWithCreatureJobInLocationsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsWithCreatureJobInLocationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .CreatureJobs.Where(j => query.LocationIds.AsEnumerable().Contains(j.LocationId))
            .Select(j => j.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
