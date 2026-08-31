using TRPG.Application.Common.Queries;
using TRPG.Application.Factions.Queries;

namespace TRPG.Application.Reputations.Queries;

public class GetEffectiveReputationQuery
{
    public required Guid ObserverCreatureId { get; init; }
    public required Guid TargetCreatureId { get; init; }
}

internal class GetEffectiveReputationQueryHandler(
    IQueryHandler<
        GetEffectiveReputationsQuery,
        IReadOnlyDictionary<Guid, int>
    > getEffectiveReputations,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds
) : IQueryHandler<GetEffectiveReputationQuery, int>
{
    public async Task<int> Handle(
        GetEffectiveReputationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var factionIdsByCreature = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = [query.TargetCreatureId] },
            cancellationToken
        );

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
