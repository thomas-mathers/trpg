using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class WildernessConnectorGenerator
{
    public static IReadOnlyList<LocationConnector> Generate(
        City city,
        District cityEntranceDistrict,
        Location wildernessLocation,
        Guid worldId
    ) =>
        [
            new LocationConnector
            {
                OriginLocationId = cityEntranceDistrict.LocationId,
                DestinationLocationId = wildernessLocation.Id,
                Name = "Path",
                Description = "A path leading into the wilderness.",
                DestinationLabel = "Wilderness",
                Direction = CompassDirection.South,
                WorldId = worldId,
            },
            new LocationConnector
            {
                OriginLocationId = wildernessLocation.Id,
                DestinationLocationId = cityEntranceDistrict.LocationId,
                Name = "Path",
                Description = $"A path leading back to {city.Name}.",
                DestinationLabel = city.Name,
                WorldId = worldId,
            },
        ];
}
