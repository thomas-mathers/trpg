using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingIdByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetBuildingIdByLocationIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetBuildingIdByLocationIdQuery, Guid?>
{
    public async Task<Guid?> Handle(
        GetBuildingIdByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from location in context.Locations.AsNoTracking()
            where location.Id == query.LocationId
            join room in context.Rooms.AsNoTracking() on location.RoomId equals room.Id
            select (Guid?)room.BuildingId
        ).FirstOrDefaultAsync(cancellationToken);
}
