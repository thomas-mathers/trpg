using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Queries;

internal class GetActiveFightQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetActiveFightQueryHandler(TrpgDbContext context)
{
    public async Task<Fight?> Handle(
        GetActiveFightQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Fights.AsNoTracking()
            .Where(f => f.WorldId == query.WorldId && f.CompletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);
}
