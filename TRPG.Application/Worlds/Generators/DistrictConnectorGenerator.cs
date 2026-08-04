using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class DistrictConnectorGenerator
{
    public static IReadOnlyList<RoomConnector> Generate(
        District cityCenterDistrict,
        IReadOnlyList<District> otherDistricts,
        Guid worldId
    )
    {
        var connectors = new List<RoomConnector>();
        foreach (var district in otherDistricts)
        {
            connectors.Add(
                new RoomConnector
                {
                    LocationId = district.LocationId,
                    DestinationLocationId = cityCenterDistrict.LocationId,
                    Name = "Path",
                    Description = $"A path leading to {cityCenterDistrict.Name}.",
                    WorldId = worldId,
                }
            );
            connectors.Add(
                new RoomConnector
                {
                    LocationId = cityCenterDistrict.LocationId,
                    DestinationLocationId = district.LocationId,
                    Name = "Path",
                    Description = $"A path leading to {district.Name}.",
                    WorldId = worldId,
                }
            );
        }
        return connectors;
    }
}
