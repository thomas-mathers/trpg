using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class HasActiveFightQuery
{
    public required Guid PlayerId { get; init; }
}

internal class HasActiveFightQueryHandler(IEncountersDbContext context)
    : IQueryHandler<HasActiveFightQuery, bool>
{
    public async Task<bool> Handle(
        HasActiveFightQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Encounters.AsNoTracking()
            .OfType<FightEncounter>()
            .AnyAsync(
                fight => fight.PlayerId == query.PlayerId && fight.State == EncounterState.Active,
                cancellationToken
            );
}
