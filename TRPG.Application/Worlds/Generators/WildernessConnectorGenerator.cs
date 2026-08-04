using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class WildernessConnectorGenerator
{
    public static IReadOnlyList<LocationConnector> Generate(
        District cityCenterDistrict,
        Location wildernessLocation,
        Guid worldId
    )
    {
        return
        [
            new LocationConnector
            {
                LocationId = cityCenterDistrict.LocationId,
                DestinationLocationId = wildernessLocation.Id,
                Name = "Path",
                Description = "A path leading into the wilderness.",
                DestinationLabel = "Wilderness",
                WorldId = worldId,
            },
            new LocationConnector
            {
                LocationId = wildernessLocation.Id,
                DestinationLocationId = cityCenterDistrict.LocationId,
                Name = "Path",
                Description = $"A path leading back to {cityCenterDistrict.Name}.",
                DestinationLabel = cityCenterDistrict.Name,
                WorldId = worldId,
            },
        ];
    }
}
