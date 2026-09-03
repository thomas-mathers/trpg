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
    Guid LocationId,
    Guid DoorConnectorId,
    IReadOnlyList<Guid> CandidateKeyItemIds
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
                room.LocationId,
                DoorConnectorId = door.Id,
            }
        ).ToListAsync(cancellationToken);

        if (doorsByRoom.Count == 0)
        {
            return [];
        }

        var doorConnectorIds = doorsByRoom.Select(d => d.DoorConnectorId).ToArray();

        var doorConnectorKeys = await context
            .DoorConnectorKeys.AsNoTracking()
            .Where(doorConnectorKey =>
                doorConnectorIds.AsEnumerable().Contains(doorConnectorKey.DoorConnectorId)
            )
            .ToArrayAsync(cancellationToken);
        var keyItemIdsByDoor = doorConnectorKeys
            .GroupBy(doorConnectorKey => doorConnectorKey.DoorConnectorId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => group.Select(k => k.ItemId).ToArray()
            );

        return doorsByRoom
            .Select(d => new GuestRoomDoor(
                d.Id,
                d.Name,
                d.LocationId,
                d.DoorConnectorId,
                keyItemIdsByDoor.GetValueOrDefault(d.DoorConnectorId, [])
            ))
            .ToArray();
    }
}
