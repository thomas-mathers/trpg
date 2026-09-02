using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public class GetReputationsByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetReputationsByCreatureIdQueryHandler(IReputationsDbContext context)
    : IQueryHandler<GetReputationsByCreatureIdQuery, IReadOnlyCollection<Reputation>>
{
    public async Task<IReadOnlyCollection<Reputation>> Handle(
        GetReputationsByCreatureIdQuery query,
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
