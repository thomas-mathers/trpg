using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetAllBuildingsByLocationQuery
{
    public required Guid StateId { get; init; }
    public required Guid? CityId { get; init; }
    public required Guid? DistrictId { get; init; }
}

internal class GetAllBuildingsByLocationQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<Building>> Handle(
        GetAllBuildingsByLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var buildings = await cache.GetOrCreateAsync<Building[]>(
            $"nearbyBuildings:{query.StateId}:{query.CityId}:{query.DistrictId}",
            async _ =>
                await context
                    .Buildings.AsNoTracking()
                    .Where(b =>
                        b.StateId == query.StateId
                        && b.CityId == query.CityId
                        && b.DistrictId == query.DistrictId
                    )
                    .ToArrayAsync(cancellationToken)
        );
        return buildings ?? [];
    }
}
