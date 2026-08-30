using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetGuestRoomDoorsByBuildingIdQuery
{
    public required Guid BuildingId { get; init; }
}

public record GuestRoomDoor(
    Guid RoomId,
    string RoomName,
    Guid DoorConnectorId,
    Guid? SpareKeyItemId,
    Guid? WorkstationId
);

internal class GetGuestRoomDoorsByBuildingIdQueryHandler(TrpgDbContext context)
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

        var spareKeysByDoor = await (
            from doorConnectorKey in context.DoorConnectorKeys.AsNoTracking()
            where doorConnectorIds.AsEnumerable().Contains(doorConnectorKey.DoorConnectorId)
            join item in context.Items.AsNoTracking() on doorConnectorKey.ItemId equals item.Id
            where item.Ownership.OwnerType == OwnerType.Workstation
            select new
            {
                doorConnectorKey.DoorConnectorId,
                KeyItemId = item.Id,
                WorkstationId = item.Ownership.OwnerId,
            }
        ).ToDictionaryAsync(k => k.DoorConnectorId, cancellationToken);

        return doorsByRoom
            .Select(d =>
            {
                spareKeysByDoor.TryGetValue(d.DoorConnectorId, out var spare);
                return new GuestRoomDoor(
                    d.Id,
                    d.Name,
                    d.DoorConnectorId,
                    spare?.KeyItemId,
                    spare?.WorkstationId
                );
            })
            .ToArray();
    }
}
