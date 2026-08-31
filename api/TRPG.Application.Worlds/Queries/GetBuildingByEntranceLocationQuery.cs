using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingByEntranceLocationQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetBuildingByEntranceLocationQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetBuildingByEntranceLocationQuery, Building?>
{
    public async Task<Building?> Handle(
        GetBuildingByEntranceLocationQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from room in context.Rooms.AsNoTracking()
            join building in context.Buildings.AsNoTracking() on room.BuildingId equals building.Id
            where room.LocationId == query.LocationId && room.FloorNumber == 0
            select building
        ).FirstOrDefaultAsync(cancellationToken);
}
