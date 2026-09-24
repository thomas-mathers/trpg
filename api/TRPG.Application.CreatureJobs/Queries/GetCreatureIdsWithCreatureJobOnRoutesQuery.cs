using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureIdsWithCreatureJobOnRoutesQuery
{
    public required IReadOnlyCollection<Guid> RouteIds { get; init; }
}

internal class GetCreatureIdsWithCreatureJobOnRoutesQueryHandler(ICreatureJobsDbContext context)
    : IQueryHandler<GetCreatureIdsWithCreatureJobOnRoutesQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsWithCreatureJobOnRoutesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.RouteIds.Count == 0)
        {
            return [];
        }

        return await context
            .CreatureJobs.AsNoTracking()
            .Where(job =>
                job.RouteId != null && query.RouteIds.AsEnumerable().Contains(job.RouteId.Value)
            )
            .Select(job => job.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
