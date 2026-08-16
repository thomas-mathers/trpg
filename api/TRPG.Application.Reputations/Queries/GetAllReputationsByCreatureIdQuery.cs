using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public class GetAllReputationsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetAllReputationsByCreatureIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllReputationsByCreatureIdQuery, IReadOnlyCollection<Reputation>>
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
