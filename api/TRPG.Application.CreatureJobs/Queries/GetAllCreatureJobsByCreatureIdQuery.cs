using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetAllCreatureJobsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

public class GetAllCreatureJobsByCreatureIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<CreatureJob>> Handle(
        GetAllCreatureJobsByCreatureIdQuery query,
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
