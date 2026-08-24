using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public record ReputationLogTarget(Guid TargetId, ReputationTargetType TargetType);

public class GetRecentReputationLogQuery
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<ReputationLogTarget> Targets { get; init; }
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
        if (query.Targets.Count == 0)
        {
            return [];
        }

        var creatureTargetIds = query
            .Targets.Where(target => target.TargetType == ReputationTargetType.Creature)
            .Select(target => target.TargetId)
            .ToArray();
        var factionTargetIds = query
            .Targets.Where(target => target.TargetType == ReputationTargetType.Faction)
            .Select(target => target.TargetId)
            .ToArray();

        var entries = context
            .ReputationLogEntries.AsNoTracking()
            .Where(e =>
                e.CreatureId == query.CreatureId
                && (
                    (
                        e.TargetType == ReputationTargetType.Creature
                        && creatureTargetIds.AsEnumerable().Contains(e.TargetId)
                    )
                    || (
                        e.TargetType == ReputationTargetType.Faction
                        && factionTargetIds.AsEnumerable().Contains(e.TargetId)
                    )
                )
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
