using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class LocationGenerator
{
    public static Location Generate(
        Guid worldId,
        Guid stateId,
        Guid? cityId = null,
        Guid? districtId = null,
        Guid? roomId = null,
        string? name = null,
        string? description = null
    ) =>
        new()
        {
            Kind =
                roomId != null ? LocationKind.Room
                : districtId != null ? LocationKind.District
                : LocationKind.Wilderness,
            Name = name ?? "",
            Description = description ?? "",
            WorldId = worldId,
            StateId = stateId,
            CityId = cityId,
            DistrictId = districtId,
            RoomId = roomId,
        };
}
