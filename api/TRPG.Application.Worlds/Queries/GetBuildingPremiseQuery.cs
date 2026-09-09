using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingPremiseQuery
{
    public required Guid BuildingId { get; init; }
}

// Read on its own rather than through the cached room result, which would serve a stale null on the
// very turn the premise is written.
internal class GetBuildingPremiseQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetBuildingPremiseQuery, string?>
{
    public async Task<string?> Handle(
        GetBuildingPremiseQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Buildings.AsNoTracking()
            .Where(building => building.Id == query.BuildingId)
            .Select(building => building.Premise)
            .FirstOrDefaultAsync(cancellationToken);
}
