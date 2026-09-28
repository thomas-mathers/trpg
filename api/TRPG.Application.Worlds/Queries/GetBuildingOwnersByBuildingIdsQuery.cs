using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingOwnersByBuildingIdsQuery
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
}

internal class GetBuildingOwnersByBuildingIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<
        GetBuildingOwnersByBuildingIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<BuildingOwner>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<BuildingOwner>>> Handle(
        GetBuildingOwnersByBuildingIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var owners = await context
            .BuildingOwners.AsNoTracking()
            .Where(owner => query.BuildingIds.AsEnumerable().Contains(owner.BuildingId))
            .ToArrayAsync(cancellationToken);

        return owners
            .GroupBy(owner => owner.BuildingId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<BuildingOwner> (group) => group.ToArray()
            );
    }
}
