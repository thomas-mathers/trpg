using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

public record BuildingIdentity(Guid Id, BuildingType BuildingType);

internal class GetBuildingByLocationIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?>
{
    public async Task<BuildingIdentity?> Handle(
        GetBuildingByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from room in context.Rooms.AsNoTracking()
            where room.LocationId == query.LocationId
            join building in context.Buildings.AsNoTracking() on room.BuildingId equals building.Id
            select new BuildingIdentity(building.Id, building.BuildingType)
        ).FirstOrDefaultAsync(cancellationToken);
}
