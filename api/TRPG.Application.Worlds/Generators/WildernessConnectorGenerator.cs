using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class WildernessConnectorGenerator
{
    private const int TravelTimeHours = 1;

    public static WildernessConnectorGeneratorResult Generate(
        City city,
        District cityEntranceDistrict,
        Location wildernessLocation,
        Guid worldId
    )
    {
        LocationConnector[] connectors =
        [
            new LocationConnector
            {
                OriginLocationId = cityEntranceDistrict.LocationId,
                DestinationLocationId = wildernessLocation.Id,
                Name = "Path",
                Description = "A path leading into the wilderness.",
                DestinationLabel = "Wilderness",
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
        var travelConnectors = connectors
            .Select(connector => new TravelConnector
            {
                ConnectorId = connector.Id,
                Distance = 1,
                TravelTimeHours = TravelTimeHours,
                WorldId = worldId,
            })
            .ToArray();

        return new WildernessConnectorGeneratorResult(connectors, travelConnectors);
    }
}

internal record WildernessConnectorGeneratorResult(
    IReadOnlyList<LocationConnector> LocationConnectors,
    IReadOnlyList<TravelConnector> TravelConnectors
);
