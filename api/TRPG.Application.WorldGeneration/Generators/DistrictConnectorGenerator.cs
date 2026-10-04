using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class DistrictConnectorGenerator
{
    private static readonly CompassDirection[] NonEntranceEdges =
    [
        CompassDirection.North,
        CompassDirection.East,
        CompassDirection.West,
    ];

    public static IReadOnlyList<LocationConnector> Generate(
        District cityCenterDistrict,
        IReadOnlyList<District> otherDistricts,
        Guid worldId
    )
    {
        var connectors = new List<LocationConnector>();
        var nextEdge = 0;
        foreach (var district in otherDistricts)
        {
            var centerEdge =
                district.DistrictType == DistrictType.CityEntrance
                    ? CompassDirection.South
                    : NonEntranceEdges[nextEdge++ % NonEntranceEdges.Length];

            connectors.Add(
                new LocationConnector
                {
                    OriginLocationId = district.LocationId,
                    DestinationLocationId = cityCenterDistrict.LocationId,
                    Name = "Path",
                    Description = $"A path leading to {cityCenterDistrict.Name}.",
                    DestinationLabel = cityCenterDistrict.Name,
                    Direction = Opposite(centerEdge),
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
                    Direction = centerEdge,
                    WorldId = worldId,
                }
            );
        }
        return connectors;
    }

    private static CompassDirection Opposite(CompassDirection direction) =>
        direction switch
        {
            CompassDirection.North => CompassDirection.South,
            CompassDirection.South => CompassDirection.North,
            CompassDirection.East => CompassDirection.West,
            CompassDirection.West => CompassDirection.East,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
}
