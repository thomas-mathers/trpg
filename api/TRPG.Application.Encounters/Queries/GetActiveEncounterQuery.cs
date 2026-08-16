using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetActiveEncounterQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveEncounterQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetActiveEncounterQuery, HostileEncounter?>
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
