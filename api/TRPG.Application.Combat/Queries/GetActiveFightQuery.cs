using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

public class GetActiveFightQuery
{
    public required Guid PlayerId { get; init; }
}

public class GetActiveFightQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetActiveFightQuery, Fight?>
{
    public async Task<Fight?> Handle(
        GetActiveFightQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Fights.AsNoTracking()
            .Where(f => f.PlayerId == query.PlayerId && f.Outcome == CombatOutcome.Ongoing)
            .FirstOrDefaultAsync(cancellationToken);
}
