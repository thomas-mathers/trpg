using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetEncounterGroupCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

public class GetEncounterGroupCreatureIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetEncounterGroupCreatureIdsQuery, IReadOnlyCollection<Guid>>
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

        var livingMembers = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(m => m.EncounterGroupId == membership.EncounterGroupId)
            .Join(
                context.Creatures.AsNoTracking().Where(c => c.State != CreatureState.Dead),
                member => member.CreatureId,
                creature => creature.Id,
                (member, creature) => new { creature.Id, creature.State }
            )
            .ToArrayAsync(cancellationToken);

        var hasAwakeMember = livingMembers.Any(m => m.State != CreatureState.Sleeping);

        return hasAwakeMember ? livingMembers.Select(m => m.Id).ToArray() : [query.CreatureId];
    }
}
