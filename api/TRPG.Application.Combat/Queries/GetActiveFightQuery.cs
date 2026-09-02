using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Queries;

public class GetActiveFightQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveFightQueryHandler(ICombatDbContext context)
    : IQueryHandler<GetActiveFightQuery, FightEncounter?>
{
    public async Task<FightEncounter?> Handle(
        GetActiveFightQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Encounters.AsNoTracking()
            .OfType<FightEncounter>()
            .Where(f => f.PlayerId == query.PlayerId && f.State == EncounterState.Active)
            .FirstOrDefaultAsync(cancellationToken);
}
