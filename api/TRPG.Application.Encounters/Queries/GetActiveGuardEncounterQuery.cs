using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetActiveGuardEncounterQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveGuardEncounterQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetActiveGuardEncounterQuery, GuardEncounter?>
{
    public async Task<GuardEncounter?> Handle(
        GetActiveGuardEncounterQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Encounters.AsNoTracking()
            .OfType<GuardEncounter>()
            .Where(e => e.PlayerId == query.PlayerId && e.State == EncounterState.Active)
            .FirstOrDefaultAsync(cancellationToken);
}
