using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetAllBuildingsByLocationQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetAllBuildingsByLocationQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<Building>> Handle(
        GetAllBuildingsByLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var buildings = await cache.GetOrCreateAsync<Building[]>(
            $"nearbyBuildings:{query.LocationId}",
            async _ =>
                await context
                    .Buildings.AsNoTracking()
                    .Where(b => b.ExteriorLocationId == query.LocationId)
                    .ToArrayAsync(cancellationToken)
        );
        return buildings ?? [];
    }
}
