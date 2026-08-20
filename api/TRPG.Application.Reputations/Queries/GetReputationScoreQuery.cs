using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public class GetReputationScoreQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid TargetId { get; init; }
    public required ReputationTargetType TargetType { get; init; }
}

internal class GetReputationScoreQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetReputationScoreQuery, int>
{
    public async Task<int> Handle(
        GetReputationScoreQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var reputation = await context
            .Reputations.AsNoTracking()
            .FirstOrDefaultAsync(
                r =>
                    r.CreatureId == query.CreatureId
                    && r.TargetId == query.TargetId
                    && r.TargetType == query.TargetType,
                cancellationToken
            );

        return reputation?.Score ?? 0;
    }
}
