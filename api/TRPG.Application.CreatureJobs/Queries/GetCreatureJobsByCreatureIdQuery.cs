using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetCreatureJobsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureJobsByCreatureIdQueryHandler(ICreatureJobsDbContext context)
    : IQueryHandler<GetCreatureJobsByCreatureIdQuery, IReadOnlyList<CreatureJob>>
{
    public async Task<IReadOnlyList<CreatureJob>> Handle(
        GetCreatureJobsByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .CreatureJobs.AsNoTracking()
            .Where(j => j.CreatureId == query.CreatureId)
            .OrderByDescending(j => j.Priority)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
