using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetBuildingByNameAtLocationQuery
{
    public required Guid LocationId { get; init; }
    public required string Name { get; init; }
}

public class GetBuildingByNameAtLocationQueryHandler(TrpgDbContext context)
{
    public async Task<Building?> Handle(
        GetBuildingByNameAtLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Buildings.AsNoTracking()
            .FirstOrDefaultAsync(
                b =>
                    b.ExteriorLocationId == query.LocationId
                    && EF.Functions.ILike(b.Name, query.Name),
                cancellationToken
            );
    }
}
