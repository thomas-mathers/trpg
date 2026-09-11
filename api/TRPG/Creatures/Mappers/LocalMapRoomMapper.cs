using TRPG.Creatures.Queries;
using TRPG.Creatures.Responses;

namespace TRPG.Creatures.Mappers;

internal static class LocalMapRoomMapper
{
    public static LocalMapRoomResponse ToResponse(this LocalMapRoom room) =>
        new(
            Id: room.Id,
            Name: room.Name,
            FloorNumber: room.FloorNumber,
            Bounds: room.Bounds?.ToMapResponse(),
            Role: room.Role,
            IsVisited: room.IsVisited,
            IsFrontier: room.IsFrontier,
            Markers: room.Markers.Select(marker => marker.ToResponse()).ToArray()
        );
}
