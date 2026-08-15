using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class DistrictConnectorGenerator
{
    public static IReadOnlyList<LocationConnector> Generate(
        District cityCenterDistrict,
        IReadOnlyList<District> otherDistricts,
        Guid worldId
    )
    {
        var connectors = new List<LocationConnector>();
        foreach (var district in otherDistricts)
        {
            connectors.Add(
                new LocationConnector
                {
                    OriginLocationId = district.LocationId,
                    DestinationLocationId = cityCenterDistrict.LocationId,
                    Name = "Path",
                    Description = $"A path leading to {cityCenterDistrict.Name}.",
                    DestinationLabel = cityCenterDistrict.Name,
                    WorldId = worldId,
                }
            );
            connectors.Add(
                new LocationConnector
                {
                    OriginLocationId = cityCenterDistrict.LocationId,
                    DestinationLocationId = district.LocationId,
                    Name = "Path",
                    Description = $"A path leading to {district.Name}.",
                    DestinationLabel = district.Name,
                    WorldId = worldId,
                }
            );
        }
        return connectors;
    }
}
