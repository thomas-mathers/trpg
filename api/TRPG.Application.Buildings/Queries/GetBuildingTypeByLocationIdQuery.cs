using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetBuildingTypeByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetBuildingTypeByLocationIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetBuildingTypeByLocationIdQuery, BuildingType?>
{
    public async Task<BuildingType?> Handle(
        GetBuildingTypeByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await (
            from room in context.Rooms.AsNoTracking()
            where room.LocationId == query.LocationId
            join building in context.Buildings.AsNoTracking() on room.BuildingId equals building.Id
            select (BuildingType?)building.BuildingType
        ).FirstOrDefaultAsync(cancellationToken);
    }
}
