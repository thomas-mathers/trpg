using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetAllBuildingsByLocationQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetAllBuildingsByLocationQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetAllBuildingsByLocationQuery, IReadOnlyCollection<Building>>
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
