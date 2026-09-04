using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetFrontDoorLockedByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

internal class GetFrontDoorLockedByBuildingIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetFrontDoorLockedByBuildingIdQuery, bool?>
{
    public async Task<bool?> Handle(
        GetFrontDoorLockedByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors on door.ConnectorId equals connector.Id
            join origin in context.Locations on connector.OriginLocationId equals origin.Id
            join room in context.Rooms on connector.DestinationLocationId equals room.LocationId
            where
                room.BuildingId == query.BuildingId
                && room.FloorNumber == 0
                && origin.RoomId == null
            select (bool?)door.IsLocked
        ).FirstOrDefaultAsync(cancellationToken);
}
