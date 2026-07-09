using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Reputations.Queries;

internal class GetAllReputationsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetAllReputationsByCreatureIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Reputation>> Handle(
        GetAllReputationsByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Reputations.AsNoTracking()
            .Where(r => r.CreatureId == query.CreatureId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
