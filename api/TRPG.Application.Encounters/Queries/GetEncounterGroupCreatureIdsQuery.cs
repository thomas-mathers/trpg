using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetEncounterGroupCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class GetEncounterGroupCreatureIdsQueryHandler(
    IEncountersDbContext context,
    IQueryHandler<
        GetCreatureConditionsByIdsQuery,
        IReadOnlyDictionary<Guid, CreatureCondition>
    > getCreatureConditionsByIds
) : IQueryHandler<GetEncounterGroupCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetEncounterGroupCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var membership = await context
            .EncounterGroupMembers.AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.WorldId == query.WorldId && m.CreatureId == query.CreatureId,
                cancellationToken
            );

        if (membership == null)
        {
            return [query.CreatureId];
        }

        var memberIds = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(m => m.EncounterGroupId == membership.EncounterGroupId)
            .Select(m => m.CreatureId)
            .ToArrayAsync(cancellationToken);

        var conditionsById = await getCreatureConditionsByIds.Handle(
            new GetCreatureConditionsByIdsQuery { Ids = memberIds },
            cancellationToken
        );

        var livingMembers = conditionsById
            .Where(kv => kv.Value != CreatureCondition.Dead)
            .ToArray();
        var hasAwakeMember = livingMembers.Any(kv => kv.Value != CreatureCondition.Sleeping);

        return hasAwakeMember ? livingMembers.Select(kv => kv.Key).ToArray() : [query.CreatureId];
    }
}
