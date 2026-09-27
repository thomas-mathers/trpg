using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetActiveFightCombatantIdsByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetActiveFightCombatantIdsByWorldQueryHandler(IEncountersDbContext context)
    : IQueryHandler<GetActiveFightCombatantIdsByWorldQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetActiveFightCombatantIdsByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var combatantIdsByFight = await context
            .Encounters.AsNoTracking()
            .OfType<FightEncounter>()
            .Where(fight => fight.WorldId == query.WorldId && fight.State == EncounterState.Active)
            .Select(fight => fight.CombatantIds)
            .ToArrayAsync(cancellationToken);

        return combatantIdsByFight.SelectMany(combatantIds => combatantIds).Distinct().ToArray();
    }
}
