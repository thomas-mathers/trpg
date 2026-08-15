using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Reputations.Queries;

public class GetEffectiveReputationQuery
{
    public required Guid ObserverCreatureId { get; init; }
    public required Guid TargetCreatureId { get; init; }
}

internal class GetEffectiveReputationQueryHandler(
    TrpgDbContext context,
    IQueryHandler<
        GetEffectiveReputationsQuery,
        IReadOnlyDictionary<Guid, int>
    > getEffectiveReputations
) : IQueryHandler<GetEffectiveReputationQuery, int>
{
    public async Task<int> Handle(
        GetEffectiveReputationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var factionIds = await context
            .FactionMembers.Where(fm => fm.CreatureId == query.TargetCreatureId)
            .Select(fm => fm.FactionId)
            .ToArrayAsync(cancellationToken);
        var factionIdsByCreature = new Dictionary<Guid, Guid[]>
        {
            [query.TargetCreatureId] = factionIds,
        };

        var reputations = await getEffectiveReputations.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = query.ObserverCreatureId,
                TargetCreatureIds = [query.TargetCreatureId],
                FactionIdsByCreature = factionIdsByCreature,
            },
            cancellationToken
        );
        return reputations.GetValueOrDefault(query.TargetCreatureId, 0);
    }
}
