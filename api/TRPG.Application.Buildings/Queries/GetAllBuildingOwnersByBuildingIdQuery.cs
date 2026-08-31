using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetAllBuildingOwnersByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetAllBuildingOwnersByBuildingIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllBuildingOwnersByBuildingIdQuery, IReadOnlyCollection<BuildingOwner>>
{
    public async Task<IReadOnlyCollection<BuildingOwner>> Handle(
        GetAllBuildingOwnersByBuildingIdQuery query,
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
