using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Queries;

public class GetEffectiveReputationsQuery
{
    public required Guid ObserverCreatureId { get; init; }
    public required IReadOnlyCollection<Guid> TargetCreatureIds { get; init; }
    public required IReadOnlyDictionary<
        Guid,
        IReadOnlyList<Guid>
    > FactionIdsByCreature { get; init; }
}

internal class GetEffectiveReputationsQueryHandler(IReputationsDbContext context)
    : IQueryHandler<GetEffectiveReputationsQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetEffectiveReputationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.TargetCreatureIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var allFactionIds = query
            .TargetCreatureIds.SelectMany(id =>
                query.FactionIdsByCreature.GetValueOrDefault(id, [])
            )
            .Distinct()
            .ToArray();

        var reputations = await context
            .Reputations.AsNoTracking()
            .Where(r =>
                r.CreatureId == query.ObserverCreatureId
                && (
                    (
                        r.TargetType == ReputationTargetType.Faction
                        && allFactionIds.Contains(r.TargetId)
                    )
                    || (
                        r.TargetType == ReputationTargetType.Creature
                        && query.TargetCreatureIds.Contains(r.TargetId)
                    )
                )
            )
            .ToArrayAsync(cancellationToken);

        var reputationByFactionId = reputations
            .Where(r => r.TargetType == ReputationTargetType.Faction)
            .ToDictionary(r => r.TargetId, r => r.Score);
        var reputationByCreatureId = reputations
            .Where(r => r.TargetType == ReputationTargetType.Creature)
            .ToDictionary(r => r.TargetId, r => r.Score);

        var result = query.TargetCreatureIds.ToDictionary(
            id => id,
            id =>
            {
                var directScore = reputationByCreatureId.GetValueOrDefault(id, 0);
                var factionScore = query
                    .FactionIdsByCreature.GetValueOrDefault(id, [])
                    .Sum(factionId => reputationByFactionId.GetValueOrDefault(factionId, 0));
                return directScore + factionScore;
            }
        );

        return result;
    }
}
