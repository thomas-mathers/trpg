using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Encounters.Queries;

internal class GetActiveEncounterQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveEncounterQueryHandler(TrpgDbContext context)
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
