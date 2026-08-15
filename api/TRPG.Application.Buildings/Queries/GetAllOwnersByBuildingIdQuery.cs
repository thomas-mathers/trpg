using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetAllOwnersByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

public class GetAllOwnersByBuildingIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllOwnersByBuildingIdQuery, IReadOnlyCollection<BuildingOwner>>
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
