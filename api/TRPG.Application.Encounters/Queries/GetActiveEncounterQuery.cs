using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetActiveEncounterQuery
{
    public required Guid PlayerId { get; init; }
}

public class GetActiveEncounterQueryHandler(TrpgDbContext context)
{
    public async Task<HostileEncounter?> Handle(
        GetActiveEncounterQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Encounters.AsNoTracking()
            .OfType<HostileEncounter>()
            .Where(e => e.PlayerId == query.PlayerId && e.State == EncounterState.Active)
            .FirstOrDefaultAsync(cancellationToken);
}
