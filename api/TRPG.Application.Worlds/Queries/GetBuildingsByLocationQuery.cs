using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingsByLocationQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetBuildingsByLocationQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetBuildingsByLocationQuery, IReadOnlyCollection<Building>>
{
    public async Task<IReadOnlyCollection<Building>> Handle(
        GetBuildingsByLocationQuery query,
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
