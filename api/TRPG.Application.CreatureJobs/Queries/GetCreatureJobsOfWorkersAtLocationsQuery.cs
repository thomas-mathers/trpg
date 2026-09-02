using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureJobsOfWorkersAtLocationsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetCreatureJobsOfWorkersAtLocationsQueryHandler(ICreatureJobsDbContext context)
    : IQueryHandler<GetCreatureJobsOfWorkersAtLocationsQuery, IReadOnlyList<CreatureJob>>
{
    public async Task<IReadOnlyList<CreatureJob>> Handle(
        GetCreatureJobsOfWorkersAtLocationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var workerIds = await context
            .CreatureJobs.Where(job =>
                job.Action == CreatureJobAction.Work
                && query.LocationIds.AsEnumerable().Contains(job.LocationId)
            )
            .Select(job => job.CreatureId)
            .ToArrayAsync(cancellationToken);

        return await context
            .CreatureJobs.AsNoTracking()
            .Where(job => workerIds.AsEnumerable().Contains(job.CreatureId))
            .ToArrayAsync(cancellationToken);
    }
}
