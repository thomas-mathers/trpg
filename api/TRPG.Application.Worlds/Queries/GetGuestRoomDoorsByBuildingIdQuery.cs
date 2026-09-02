using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetGuestRoomDoorsByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

public record GuestRoomDoor(
    Guid RoomId,
    string RoomName,
    Guid DoorConnectorId,
    Guid? CandidateKeyItemId
);

internal class GetGuestRoomDoorsByBuildingIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetGuestRoomDoorsByBuildingIdQuery, IReadOnlyList<GuestRoomDoor>>
{
    public async Task<IReadOnlyList<GuestRoomDoor>> Handle(
        GetGuestRoomDoorsByBuildingIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var doorsByRoom = await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            join room in context.Rooms.AsNoTracking()
                on connector.DestinationLocationId equals room.LocationId
            where room.BuildingId == query.BuildingId
            select new
            {
                room.Id,
                room.Name,
                DoorConnectorId = door.Id,
            }
        ).ToListAsync(cancellationToken);

        if (doorsByRoom.Count == 0)
        {
            return [];
        }

        var doorConnectorIds = doorsByRoom.Select(d => d.DoorConnectorId).ToArray();

        var keyItemIdsByDoor = await context
            .DoorConnectorKeys.AsNoTracking()
            .Where(doorConnectorKey =>
                doorConnectorIds.AsEnumerable().Contains(doorConnectorKey.DoorConnectorId)
            )
            .ToDictionaryAsync(
                doorConnectorKey => doorConnectorKey.DoorConnectorId,
                doorConnectorKey => (Guid?)doorConnectorKey.ItemId,
                cancellationToken
            );

        return doorsByRoom
            .Select(d =>
            {
                keyItemIdsByDoor.TryGetValue(d.DoorConnectorId, out var keyItemId);
                return new GuestRoomDoor(d.Id, d.Name, d.DoorConnectorId, keyItemId);
            })
            .ToArray();
    }
}
