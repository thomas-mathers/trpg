using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Queries;

public record EncounterGroupContext(
    EncounterGroup Group,
    Faction Faction,
    IReadOnlyList<Creature> LivingMembers
);

public class GetEncounterGroupContextQuery
{
    public required Guid EncounterGroupId { get; init; }
}

public class GetEncounterGroupContextQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetEncounterGroupContextQuery, EncounterGroupContext>
{
    public async Task<EncounterGroupContext> Handle(
        GetEncounterGroupContextQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var group = await context
            .EncounterGroups.AsNoTracking()
            .FirstAsync(g => g.Id == query.EncounterGroupId, cancellationToken);

        var faction = await context
            .Factions.AsNoTracking()
            .FirstAsync(f => f.Id == group.FactionId, cancellationToken);

        var memberCreatureIds = await context
            .EncounterGroupMembers.AsNoTracking()
            .Where(m => m.EncounterGroupId == query.EncounterGroupId)
            .Select(m => m.CreatureId)
            .ToArrayAsync(cancellationToken);

        var livingMembers = await context
            .Creatures.AsNoTracking()
            .Where(c =>
                memberCreatureIds.AsEnumerable().Contains(c.Id) && c.State != CreatureState.Dead
            )
            .ToArrayAsync(cancellationToken);

        return new EncounterGroupContext(group, faction, livingMembers);
    }
}
