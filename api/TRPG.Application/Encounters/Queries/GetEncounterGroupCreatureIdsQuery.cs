using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Queries;

internal class GetEncounterGroupCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class GetEncounterGroupCreatureIdsQueryHandler(TrpgDbContext context)
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

        return await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(m => m.EncounterGroupId == membership.EncounterGroupId)
            .Join(
                context.Creatures.AsNoTracking().Where(c => c.State != CreatureState.Dead),
                member => member.CreatureId,
                creature => creature.Id,
                (member, creature) => creature.Id
            )
            .ToArrayAsync(cancellationToken);
    }
}
