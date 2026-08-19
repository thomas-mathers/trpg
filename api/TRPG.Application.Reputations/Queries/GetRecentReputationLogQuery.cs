using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public class GetRecentReputationLogQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid TargetId { get; init; }
    public required ReputationTargetType TargetType { get; init; }
    public required int Limit { get; init; }
    public bool NegativeOnly { get; init; }
}

internal class GetRecentReputationLogQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetRecentReputationLogQuery, IReadOnlyCollection<ReputationLogEntry>>
{
    public async Task<IReadOnlyCollection<ReputationLogEntry>> Handle(
        GetRecentReputationLogQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entries = context
            .ReputationLogEntries.AsNoTracking()
            .Where(e =>
                e.CreatureId == query.CreatureId
                && e.TargetId == query.TargetId
                && e.TargetType == query.TargetType
            );

        if (query.NegativeOnly)
        {
            entries = entries.Where(e => e.DeltaScore < 0);
        }

        return await entries
            .OrderByDescending(e => e.CreatedAt)
            .Take(query.Limit)
            .ToArrayAsync(cancellationToken);
    }
}
