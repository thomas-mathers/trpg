using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class LocationCoarseAnchorGenerator
{
    public static IReadOnlyList<Location> Generate(
        IReadOnlyCollection<Location> locations,
        IReadOnlyCollection<District> districts,
        IReadOnlyDictionary<Guid, Location> wildernessLocationByStateId
    )
    {
        var cityEntranceLocationIdByCityId = districts
            .Where(district => district.DistrictType == DistrictType.CityEntrance)
            .ToDictionary(district => district.CityId, district => district.LocationId);

        return locations
            .Select(location => new Location
            {
                Id = location.Id,
                Kind = location.Kind,
                Name = location.Name,
                Description = location.Description,
                WorldId = location.WorldId,
                StateId = location.StateId,
                CityId = location.CityId,
                DistrictId = location.DistrictId,
                RoomId = location.RoomId,
                CoarseAnchorLocationId = ResolveAnchor(
                    location,
                    cityEntranceLocationIdByCityId,
                    wildernessLocationByStateId
                ),
            })
            .ToArray();
    }

    private static Guid ResolveAnchor(
        Location location,
        IReadOnlyDictionary<Guid, Guid> cityEntranceLocationIdByCityId,
        IReadOnlyDictionary<Guid, Location> wildernessLocationByStateId
    )
    {
        if (
            location.CityId is { } cityId
            && cityEntranceLocationIdByCityId.TryGetValue(cityId, out var cityEntranceLocationId)
        )
        {
            return cityEntranceLocationId;
        }

        if (location.Kind == LocationKind.Wilderness)
        {
            return location.Id;
        }

        return wildernessLocationByStateId[location.StateId].Id;
    }
}
