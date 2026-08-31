using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetEncounterGroupCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class GetEncounterGroupCreatureIdsQueryHandler(
    TrpgDbContext context,
    IQueryHandler<
        GetCreatureStatesByIdsQuery,
        IReadOnlyDictionary<Guid, CreatureState>
    > getCreatureStatesByIds
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

        var statesById = await getCreatureStatesByIds.Handle(
            new GetCreatureStatesByIdsQuery { Ids = memberIds },
            cancellationToken
        );

        var livingMembers = statesById.Where(kv => kv.Value != CreatureState.Dead).ToArray();
        var hasAwakeMember = livingMembers.Any(kv => kv.Value != CreatureState.Sleeping);

        return hasAwakeMember ? livingMembers.Select(kv => kv.Key).ToArray() : [query.CreatureId];
    }
}
