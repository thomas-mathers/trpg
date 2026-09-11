using TRPG.Creatures.Queries;
using TRPG.Creatures.Responses;

namespace TRPG.Creatures.Mappers;

internal static class LocalMapResultMapper
{
    public static LocalMapResponse ToResponse(this LocalMapResult map) =>
        new(
            BuildingId: map.BuildingId,
            BuildingName: map.BuildingName,
            BuildingType: map.BuildingType,
            CurrentRoomId: map.CurrentRoomId,
            Rooms: map.Rooms.Select(room => room.ToResponse()).ToArray(),
            Passages: map.Passages.Select(passage => passage.ToResponse()).ToArray()
        );
}
