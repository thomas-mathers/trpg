using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureJobsByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetCreatureJobsByCreatureIdsQueryHandler(ICreatureJobsDbContext context)
    : IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>> Handle(
        GetCreatureJobsByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CreatureJob>>();
        }

        var jobs = await context
            .CreatureJobs.AsNoTracking()
            .Where(job => query.CreatureIds.AsEnumerable().Contains(job.CreatureId))
            .OrderByDescending(job => job.Priority)
            .ToArrayAsync(cancellationToken);

        return jobs.GroupBy(job => job.CreatureId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<CreatureJob> (group) => group.ToArray()
            );
    }
}
