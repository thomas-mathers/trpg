using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingOwnersByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetBuildingOwnersByBuildingIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetBuildingOwnersByBuildingIdQuery, IReadOnlyCollection<BuildingOwner>>
{
    public async Task<IReadOnlyCollection<BuildingOwner>> Handle(
        GetBuildingOwnersByBuildingIdQuery query,
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
