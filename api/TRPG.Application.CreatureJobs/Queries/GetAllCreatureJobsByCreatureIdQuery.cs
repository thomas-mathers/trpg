using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetAllCreatureJobsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetAllCreatureJobsByCreatureIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllCreatureJobsByCreatureIdQuery, IReadOnlyList<CreatureJob>>
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
