using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetWorkstationsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

public class GetWorkstationsByLocationIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Workstation>> Handle(
        GetWorkstationsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == query.LocationId)
            .OfType<Workstation>()
            .ToArrayAsync(cancellationToken);
    }
}
