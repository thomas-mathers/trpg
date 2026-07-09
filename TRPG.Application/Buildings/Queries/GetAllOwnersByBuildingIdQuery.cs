using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetAllOwnersByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetAllOwnersByBuildingIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<BuildingOwner>> Handle(
        GetAllOwnersByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .BuildingOwners.AsNoTracking()
            .Where(o => o.BuildingId == query.BuildingId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
