using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Jobs.Queries;

internal class GetAllJobsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetAllJobsByCreatureIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Job>> Handle(
        GetAllJobsByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Jobs.AsNoTracking()
            .Where(j => j.CreatureId == query.CreatureId)
            .OrderByDescending(j => j.Priority)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
